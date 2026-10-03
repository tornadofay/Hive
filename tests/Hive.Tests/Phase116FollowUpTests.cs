using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;
using Hive.Providers.OpenAICompatible;
using Xunit;

namespace Hive.Tests;

public sealed class Phase116FollowUpTests
{
    [Fact]
    public async Task OpenAICompatibleAdapter_ParsesOpenRouterModelMetadata()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "openai/gpt-4",
                      "canonical_slug": "openai/gpt-4",
                      "name": "GPT-4",
                      "description": "A multimodal model.",
                      "created": 1692901234,
                      "architecture": {
                        "input_modalities": ["text", "image"],
                        "modality": "text+image->text",
                        "output_modalities": ["text"],
                        "instruct_type": "chatml",
                        "tokenizer": "GPT"
                      },
                      "context_length": 8192,
                      "pricing": {
                        "completion": "0.00006",
                        "prompt": "0.00003",
                        "image": "0",
                        "request": "0"
                      },
                      "supported_parameters": [
                        "temperature",
                        "tools",
                        "structured_outputs",
                        "reasoning"
                      ],
                      "top_provider": {
                        "is_moderated": true,
                        "context_length": 8192,
                        "max_completion_tokens": 4096
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://openrouter.ai/api/v1/")));

        var result = await adapter.ListModelsAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);

        var model = Assert.Single(result.Value!.Models);

        Assert.Equal("openai/gpt-4", model.Id);
        Assert.Equal("GPT-4", model.DisplayName);
        Assert.Equal("A multimodal model.", model.Description);
        Assert.Equal(
            ["image", "text"],
            model.InputModalities);
        Assert.Equal(
            ["text"],
            model.OutputModalities);

        Assert.Contains(
            model.Capabilities,
            capability =>
                capability.Key == "vision" &&
                capability.State == CapabilityState.Supported);
        Assert.Contains(
            model.Capabilities,
            capability =>
                capability.Key == "text.generate" &&
                capability.State == CapabilityState.Supported);
        Assert.Contains(
            model.Capabilities,
            capability =>
                capability.Key == "tool.calling" &&
                capability.State == CapabilityState.Supported);
        Assert.Contains(
            model.Capabilities,
            capability =>
                capability.Key == "structured.output" &&
                capability.State == CapabilityState.Supported);
        Assert.Contains(
            model.Capabilities,
            capability =>
                capability.Key == "reasoning" &&
                capability.State == CapabilityState.Supported);

        Assert.Equal(8192, model.Limits!.ContextWindowTokens);
        Assert.Equal(4096, model.Limits.MaxOutputTokens);
        Assert.Equal(
            0.00003m,
            model.Pricing!.Prices.Single(
                price => price.BillingUnit == "input_token").Price);
        Assert.Equal(
            0.00006m,
            model.Pricing.Prices.Single(
                price => price.BillingUnit == "output_token").Price);

        Assert.NotNull(model.ExtensionData);
        Assert.True(model.ExtensionData!.ContainsKey("architecture"));
        Assert.True(model.ExtensionData.ContainsKey("supported_parameters"));
    }

    [Fact]
    public async Task OpenAICompatibleAdapter_PreservesRichModelMetadata_AndRedactsSensitiveExtensionEvidence()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "rich-model",
                      "owned_by": "example-provider",
                      "created": 1577836800,
                      "available": true,
                      "health": "healthy",
                      "family": "example-family",
                      "type": "chat",
                      "category": "multimodal",
                      "version": "1.0",
                      "name": "Rich Model",
                      "description": "Deterministic rich model fixture.",
                      "state": "active",
                      "input_modalities": ["text", "image", "audio"],
                      "output_modalities": ["text"],
                      "supports_vision": true,
                      "supports_tools": true,
                      "supports_structured_output": false,
                      "supports_reasoning": true,
                      "thinking": {
                        "options": ["low", "medium", "high"],
                        "default": "medium"
                      },
                      "limits": {
                        "context_length": 131072,
                        "max_input_tokens": 120000,
                        "max_output_tokens": 8192,
                        "max_images_per_request": 16,
                        "authorization": "Bearer SECRET"
                      },
                      "pricing": {
                        "currency": "USD",
                        "input": {
                          "price": 1.25,
                          "unit": "input_token",
                          "unit_quantity": 1000000
                        },
                        "output": {
                          "price": 5.0,
                          "unit": "output_token",
                          "unit_quantity": 1000000
                        }
                      },
                      "capabilities": {
                        "provider.feature": true,
                        "structured.output": false
                      },
                      "provider_metadata": {
                        "name": "fixture",
                        "authorization": "Bearer SECRET",
                        "url": "https://example.test/?api_key=secret"
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var result = await adapter.ListModelsAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);

        var model = Assert.Single(result.Value!.Models);

        Assert.Equal("rich-model", model.Id);
        Assert.Equal("example-provider", model.OwnedBy);
        Assert.Equal("example-family", model.Family);
        Assert.Equal("chat", model.ModelType);
        Assert.Equal("multimodal", model.Category);
        Assert.Equal("1.0", model.Version);
        Assert.Equal("Rich Model", model.DisplayName);
        Assert.Equal("Deterministic rich model fixture.", model.Description);
        Assert.Equal("active", model.OperationalState);

        Assert.Equal(
            ["audio", "image", "text"],
            model.InputModalities);
        Assert.Equal(
            ["text"],
            model.OutputModalities);
        Assert.Equal(
            ["low", "medium", "high"],
            model.ThinkingOptions);
        Assert.Equal("medium", model.DefaultThinkingLevel);

        Assert.Contains(
            model.Capabilities,
            capability =>
                capability.Key == "vision" &&
                capability.State == CapabilityState.Supported);
        Assert.Contains(
            model.Capabilities,
            capability =>
                capability.Key == "structured.output" &&
                capability.State == CapabilityState.Unsupported);
        Assert.Contains(
            model.Capabilities,
            capability =>
                capability.Key == "reasoning" &&
                capability.State == CapabilityState.Supported);

        Assert.Equal(131072, model.Limits!.ContextWindowTokens);
        Assert.Equal(120000, model.Limits.MaxInputTokens);
        Assert.Equal(8192, model.Limits.MaxOutputTokens);
        Assert.True(model.Limits.AdditionalConstraints.ContainsKey("max_images_per_request"));
        Assert.False(model.Limits.AdditionalConstraints.ContainsKey("authorization"));

        Assert.False(model.Pricing!.ExplicitFreeEvidence);
        Assert.Equal(2, model.Pricing.Prices.Count);
        Assert.Equal(
            1.25m,
            model.Pricing.Prices.Single(
                price => price.BillingUnit == "input_token").Price);
        Assert.Equal(
            5m,
            model.Pricing.Prices.Single(
                price => price.BillingUnit == "output_token").Price);

        Assert.NotNull(model.ExtensionData);
        var providerMetadata = Assert.IsType<JsonElement>(
            model.ExtensionData!["provider_metadata"]);
        Assert.Equal("fixture", providerMetadata.GetProperty("name").GetString());
        Assert.False(providerMetadata.TryGetProperty("authorization", out _));
        Assert.False(providerMetadata.TryGetProperty("url", out _));

        Assert.True(model.ExtensionData!.ContainsKey("capabilities"));
    }

    [Fact]
    public void ProviderModelMetadata_AllowsBoundedLongDescription()
    {
        var description = new string('d', 1024);

        var metadata = new ProviderModelMetadata(
            "long-description-model",
            "example",
            null,
            ProviderAvailabilityStatus.Available,
            ProviderHealthStatus.Healthy,
            [],
            description: description);

        Assert.Equal(description, metadata.Description);

        var tooLong = new string('d', 4097);

        Assert.Throws<ArgumentException>(
            () => new ProviderModelMetadata(
                "too-long-description-model",
                "example",
                null,
                ProviderAvailabilityStatus.Available,
                ProviderHealthStatus.Healthy,
                [],
                description: tooLong));
    }

    [Fact]
    public async Task OpenAICompatibleAdapter_MissingPricingIsUnknown_WhileExplicitFreeEvidenceIsPreserved()
    {
        using var missingPriceClient = new HttpClient(
            new FixedResponseHandler(
                """
                {"data":[{"id":"missing-price"}]}
                """));

        var missingPriceAdapter = new OpenAICompatibleProviderAdapter(
            missingPriceClient,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var missingPrice = await missingPriceAdapter.ListModelsAsync();

        Assert.True(missingPrice.IsSuccess, missingPrice.Error?.Message);
        Assert.Null(Assert.Single(missingPrice.Value!.Models).Pricing);

        using var freeClient = new HttpClient(
            new FixedResponseHandler(
                """
                {"data":[{"id":"free-model","free":true}]}
                """));

        var freeAdapter = new OpenAICompatibleProviderAdapter(
            freeClient,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var free = await freeAdapter.ListModelsAsync();

        Assert.True(free.IsSuccess, free.Error?.Message);
        Assert.True(
            Assert.Single(free.Value!.Models)
                .Pricing!.ExplicitFreeEvidence);
    }

    [Fact]
    public async Task OpenAICompatibleAdapter_ZeroPricedEntryCountsAsExplicitFreeEvidence()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {"data":[{"id":"free-by-price","pricing":{"input":0,"output":0}}]}
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var result = await adapter.ListModelsAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(
            Assert.Single(result.Value!.Models)
                .Pricing!.ExplicitFreeEvidence);
    }

    [Fact]
    public void BuiltInProviderCatalog_DistinguishesNoOptionalAndRequiredCredentialRequirements()
    {
        var ollama = Assert.IsType<BuiltInProviderDefinition>(
            BuiltInProviderCatalog.Find("ollama"));
        var lmStudio = Assert.IsType<BuiltInProviderDefinition>(
            BuiltInProviderCatalog.Find("lm-studio"));
        var openAi = Assert.IsType<BuiltInProviderDefinition>(
            BuiltInProviderCatalog.Find("openai"));
        var cloudflare = Assert.IsType<BuiltInProviderDefinition>(
            BuiltInProviderCatalog.Find("cloudflare"));

        Assert.Equal(
            BuiltInProviderCredentialRequirement.Optional,
            ollama.CredentialRequirement);
        Assert.Equal(
            BuiltInProviderCredentialRequirement.Optional,
            lmStudio.CredentialRequirement);
        Assert.False(ollama.RequiresCredential);

        Assert.Equal(
            BuiltInProviderCredentialRequirement.Required,
            openAi.CredentialRequirement);
        Assert.True(openAi.RequiresCredential);

        Assert.False(cloudflare.NormalOnboardingSupported);
        Assert.Equal(
            BuiltInProviderCredentialRequirement.Required,
            cloudflare.CredentialRequirement);
    }

    [WinFormsFact]
    public void StructuredCapabilityEditor_PreservesConfiguredOverridesAndDiscoveryEvidence()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        using var editor = new HiveCapabilityEditor(themeManager);

        var configured = new[]
        {
            new CapabilityStateEntry(
                HiveCapabilityKeys.Vision,
                CapabilityState.Unsupported)
        };

        var discovery = CreateRichModelFixture().Model;

        editor.Configure(
            configured,
            discovery,
            automatic: false);

        var result = editor.GetConfiguredCapabilities();

        var vision = Assert.Single(
            result,
            item => item.Capability == HiveCapabilityKeys.Vision);

        Assert.Equal(CapabilityState.Unsupported, vision.State);
        Assert.Equal(
            CapabilityState.Supported,
            discovery.DiscoveredCapabilities
                .Single(item => item.Capability == HiveCapabilityKeys.Vision)
                .State);
    }

    [WinFormsFact]
    public void AdvancedConfiguration_UsesHiveTabsWithoutOverview()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        var context = CreateContext();
        var management = CreateUnusedManagementProxy();

        using var form = new HiveAdvancedProviderConfigurationForm(
            management,
            context,
            themeManager);

        Assert.Equal(
            "Advanced Provider Configuration",
            form.Text);

        Assert.IsType<HiveTabControl>(form.NavigationTabs);

        var names = form.NavigationTabs.TabPages
            .Cast<TabPage>()
            .Select(page => page.Text)
            .ToArray();

        Assert.Equal(
            [
                "Providers",
                "Accounts / Credentials",
                "Execution Targets",
                "Model Information"
            ],
            names);

        Assert.Equal(
            "Providers",
            form.NavigationTabs.SelectedTab?.Text);

        Assert.All(
            form.NavigationTabs.TabPages.Cast<TabPage>(),
            page =>
            {
                Assert.NotNull(page.Tag);
                Assert.Equal(page.Name, page.Tag.ToString());
            });
    }

    [WinFormsFact]
    public async Task ModelInformationView_RendersRichDiscoveryProfile()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        var fixture = CreateFixture();

        using var host = new Form
        {
            Size = new Size(1160, 760)
        };
        using var view = new HiveModelInformationSettingsView(
            fixture.Management,
            fixture.Context,
            themeManager);

        host.Controls.Add(view);
        host.Show();
        Application.DoEvents();

        await view.InitializeAsync();
        Application.DoEvents();

        Assert.IsType<HiveComboBox>(view.ProviderSelector);
        Assert.IsType<HiveComboBox>(view.AccountSelector);
        Assert.IsType<HiveComboBox>(view.EndpointSelector);
        Assert.Equal(view.ProviderSelector.Height, view.AccountSelector.Height);
        Assert.Equal(view.ProviderSelector.Height, view.EndpointSelector.Height);
        Assert.InRange(view.ProviderSelector.Height, 34, 72);
        Assert.IsType<HiveListView>(view.ModelsList);
        Assert.Same(view.ModelsList, view.CrudPage.ListView);
        Assert.Same(view.ModelsList, view.CrudPage.ListScrollHostForTesting.Content);
        Assert.Same(view.DetailsContent, view.DetailsScrollHost.Content);
        Assert.Equal("Model Information", view.CrudPage.Title);
        Assert.False(view.CrudPage.AllowEdit);
        Assert.False(view.CrudPage.AllowDelete);
        Assert.False(view.CrudPage.ShowRefresh);
        Assert.True(view.CrudPage.AllowAdd);
        Assert.Equal("Add to Favorites", view.CrudPage.AddButtonText);

        Assert.Equal(
            [
                "Model",
                "Text",
                "Vision",
                "Tools",
                "Structured",
                "Reasoning",
                "Thinking"
            ],
            view.CrudPage.Columns.Select(column => column.Header));

        Assert.Equal(2, view.ModelsList.Items.Count);
        Assert.Equal(
            "rich-model",
            view.ModelsList.Items[0].Text);

        view.ModelsList.Items[0].Selected = true;
        view.ModelsList.Items[0].Focused = true;
        Application.DoEvents();

        var detailsText = CollectVisibleControlText(view.DetailsContent);

        Assert.Contains("rich-model", detailsText);
        Assert.Contains("example-family", detailsText);
        Assert.Contains("text, image, audio", detailsText);
        Assert.Contains("Capabilities", detailsText);
        Assert.Contains("text.generate", detailsText);
        Assert.Contains("Supported  •  discovered", detailsText);
        Assert.Contains("Reasoning", detailsText);
        Assert.Contains("medium", detailsText);
        Assert.Contains("Limits", detailsText);
        Assert.Contains("131072", detailsText);
        Assert.Contains("Pricing & economics", detailsText);
        Assert.Contains("1.25", detailsText);
        Assert.Contains("Operational state", detailsText);
        Assert.Contains("active", detailsText);
        Assert.Contains("Additional provider information", detailsText);
        Assert.Contains("deterministic-fixture", detailsText);

        var detailCards = view.DetailsContent.Controls
            .Cast<Control>()
            .ToArray();

        Assert.NotEmpty(detailCards);
        Assert.All(
            detailCards,
            control =>
            {
                var panel = Assert.IsType<Panel>(control);
                Assert.True(panel.Width >= 320);
                Assert.True(panel.Height >= 48);
                Assert.Equal(
                    BorderStyle.FixedSingle,
                    panel.BorderStyle);

                var table = Assert.IsType<TableLayoutPanel>(
                    Assert.Single(panel.Controls.Cast<Control>()));

                Assert.True(table.Width >= 300);
                Assert.True(table.Height > 28);
            });

        var detailBounds = detailCards
            .Select(control => control.Bounds)
            .ToArray();

        Assert.Equal(
            detailBounds.Length,
            detailBounds.Select(bounds => bounds.Top).Distinct().Count());
    }

    [WinFormsFact]
    public async Task ModelInformationView_AddsSelectedExecutionTargetToFavorites()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        var fixture = CreateFixture();

        using var host = new Form
        {
            Size = new Size(1160, 760)
        };
        using var view = new HiveModelInformationSettingsView(
            fixture.Management,
            fixture.Context,
            themeManager);

        host.Controls.Add(view);
        host.Show();
        Application.DoEvents();

        await view.InitializeAsync();
        Application.DoEvents();

        var selected = view.ModelsList.Items[0];
        selected.Selected = true;
        selected.Focused = true;

        var actionLayout = Assert.IsType<TableLayoutPanel>(
            view.CrudPage.ActionBarPanel.Controls[0]);
        var actionButtons = actionLayout.Controls
            .OfType<FlowLayoutPanel>()
            .Single();
        var addButton = actionButtons.Controls
            .OfType<HiveButton>()
            .Single(button => button.Text == "Add to Favorites");

        addButton.PerformClick();
        Application.DoEvents();

        Assert.Contains(fixture.Target.Id, fixture.ManagementProxy.FavoriteExecutionTargetIds);
        Assert.StartsWith("★ ", view.ModelsList.Items[0].Text);
    }

    [WinFormsFact]
    public async Task ModelInformationView_UpdatesDetailsWhenSelectionChanges()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        var fixture = CreateFixture();

        using var host = new Form
        {
            Size = new Size(1160, 760)
        };
        using var view = new HiveModelInformationSettingsView(
            fixture.Management,
            fixture.Context,
            themeManager);

        host.Controls.Add(view);
        host.Show();
        Application.DoEvents();

        await view.InitializeAsync();
        Application.DoEvents();

        Assert.Equal(2, view.ModelsList.Items.Count);
        var firstItem = view.ModelsList.Items[0];
        var secondItem = view.ModelsList.Items[1];

        Assert.NotEqual(firstItem.Text, secondItem.Text);

        firstItem.Selected = false;
        secondItem.Selected = true;
        secondItem.Focused = true;
        Application.DoEvents();

        var detailsText = CollectVisibleControlText(view.DetailsContent);

        Assert.Contains("second-model", detailsText);
        Assert.Contains("second-provider", detailsText);
        Assert.DoesNotContain("rich-model", detailsText);
    }

    private static string CollectVisibleControlText(Control root)
    {
        var values = new List<string>();

        foreach (Control control in root.Controls)
        {
            if (!string.IsNullOrWhiteSpace(control.Text))
                values.Add(control.Text);

            if (control.HasChildren)
                values.Add(CollectVisibleControlText(control));
        }

        return string.Join(
            Environment.NewLine,
            values);
    }

    private static ResourceAccessContext CreateContext() =>
        new(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

    private static IHiveManagementFacade CreateUnusedManagementProxy()
    {
        return DispatchProxy.Create<
            IHiveManagementFacade,
            ThrowingManagementProxy>();
    }

    private sealed record TestModelFixture(ProviderModelMetadata Model);

    private static TestModelFixture CreateRichModelFixture()
    {
        var model = new ProviderModelMetadata(
            "rich-model",
            "example-provider",
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
            ProviderAvailabilityStatus.Available,
            ProviderHealthStatus.Healthy,
            [
                new CapabilityStateEntry(HiveCapabilityKeys.TextGeneration, CapabilityState.Supported),
                new CapabilityStateEntry(HiveCapabilityKeys.Vision, CapabilityState.Supported),
                new CapabilityStateEntry(HiveCapabilityKeys.ToolCalling, CapabilityState.Supported),
                new CapabilityStateEntry(HiveCapabilityKeys.StructuredOutput, CapabilityState.Supported),
                new CapabilityStateEntry(HiveCapabilityKeys.Reasoning, CapabilityState.Supported),
                new CapabilityStateEntry(HiveCapabilityKeys.Thinking, CapabilityState.Supported)
            ],
            ["text"],
            ["text"],
            family: "example-family",
            modelType: "chat",
            category: "multimodal",
            version: "1.0",
            operationalState: "active",
            thinkingOptions: ["low", "medium", "high"],
            defaultThinkingLevel: "medium");

        return new TestModelFixture(model);
    }

    private static ModelInformationFixture CreateFixture(
        bool favoriteFirstModel = false)
    {
        var context = CreateContext();
        var principal = context.PrincipalId!.Value;
        var tenant = context.TenantId!.Value;
        var created = new DateTimeOffset(
            2030,
            1,
            2,
            3,
            4,
            5,
            TimeSpan.Zero);

        var provider = new Provider(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                ProviderId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    created,
                    CorrelationId.New()),
                ResourceLifecycle.Active(created)),
            "model-information-provider",
            "Model Information Provider",
            "openai-compatible");

        var account = new ProviderAccount(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    created,
                    CorrelationId.New()),
                ResourceLifecycle.Active(created)),
            provider.Id,
            "model-information-account",
            "Model Information Account",
            "deterministic-fixture");

        var target = new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    created,
                    CorrelationId.New()),
                ResourceLifecycle.Active(created)),
            provider.Id,
            account.Id,
            "model-information-target",
            "Model Information Target",
            new Uri("https://example.test/v1/"),
            "rich-model",
            null,
            []);

        var secondTarget = new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    created,
                    CorrelationId.New()),
                ResourceLifecycle.Active(created)),
            provider.Id,
            account.Id,
            "second-model-information-target",
            "Second Model Information Target",
            target.Endpoint,
            "second-model",
            null,
            []);

        var observed = created.AddMinutes(5);
        var staleAfter = observed.AddHours(1);

        var model = new ProviderModelMetadata(
            "rich-model",
            "example-provider",
            created,
            ProviderAvailabilityStatus.Available,
            ProviderHealthStatus.Healthy,
            [
                new CapabilityStateEntry(HiveCapabilityKeys.TextGeneration, CapabilityState.Supported),
                new CapabilityStateEntry(HiveCapabilityKeys.Vision, CapabilityState.Supported),
                new CapabilityStateEntry(HiveCapabilityKeys.ToolCalling, CapabilityState.Supported),
                new CapabilityStateEntry(HiveCapabilityKeys.StructuredOutput, CapabilityState.Supported),
                new CapabilityStateEntry(HiveCapabilityKeys.Reasoning, CapabilityState.Supported),
                new CapabilityStateEntry(HiveCapabilityKeys.Thinking, CapabilityState.Supported)
            ],
            ["text", "image", "audio"],
            ["text"],
            family: "example-family",
            modelType: "chat",
            category: "multimodal",
            version: "1.0",
            operationalState: "active",
            thinkingOptions: ["low", "medium", "high"],
            defaultThinkingLevel: "medium",
            limits: new ProviderModelLimits(
                131072,
                120000,
                8192,
                new Dictionary<string, JsonElement>
                {
                    ["max_images_per_request"] =
                        JsonSerializer.SerializeToElement(16)
                }),
            pricing: new ProviderModelPricing(
                [
                    new ProviderModelPrice("input_token", 1.25m, "USD", 1_000_000m),
                    new ProviderModelPrice("output_token", 5m, "USD", 1_000_000m)
                ]),
            extensionData: new Dictionary<string, JsonElement>
            {
                ["vendor_library"] =
                    JsonSerializer.SerializeToElement("deterministic-fixture")
            },
            observedAtUtc: observed,
            staleAfterUtc: staleAfter);

        var secondModel = new ProviderModelMetadata(
            "second-model",
            "second-provider",
            created,
            ProviderAvailabilityStatus.Available,
            ProviderHealthStatus.Healthy,
            [
                new CapabilityStateEntry(
                    HiveCapabilityKeys.TextGeneration,
                    CapabilityState.Supported),
                new CapabilityStateEntry(
                    HiveCapabilityKeys.Reasoning,
                    CapabilityState.Unknown)
            ],
            ["text"],
            ["text"],
            family: "second-family",
            modelType: "chat",
            operationalState: "active");

        var snapshot = new ProviderDiscoverySnapshot(
            provider.Id,
            account.Id,
            target.Endpoint,
            new ProviderOperationalMetadata(
                ProviderAvailabilityStatus.Available,
                ProviderHealthStatus.Healthy,
                observed,
                staleAfter,
                17),
            ProviderDiscoveryState.Supported,
            [model, secondModel]);

        var management = DispatchProxy.Create<
            IHiveManagementFacade,
            ModelInformationManagementProxy>();

        var managementProxy =
            (ModelInformationManagementProxy)(object)management;

        managementProxy.Configure(
            provider,
            account,
            [target, secondTarget],
            snapshot,
            context,
            favoriteFirstModel
                ? [target.Id]
                : []);

        return new ModelInformationFixture(
            context,
            management,
            target,
            managementProxy);
    }

    private sealed record ModelInformationFixture(
        ResourceAccessContext Context,
        IHiveManagementFacade Management,
        ExecutionTarget Target,
        ModelInformationManagementProxy ManagementProxy);

    private class ThrowingManagementProxy : DispatchProxy
    {
        protected override object? Invoke(
            System.Reflection.MethodInfo? targetMethod,
            object?[]? args) =>
            throw new NotSupportedException(
                $"No management operation is expected for the Advanced Provider Configuration navigation fixture: {targetMethod?.Name}");
    }

    private class ModelInformationManagementProxy : DispatchProxy
    {
        private Provider? _provider;
        private ProviderAccount? _account;
        private IReadOnlyList<ExecutionTarget> _targets = [];
        private ProviderDiscoverySnapshot? _snapshot;
        private IReadOnlyList<ExecutionTargetId> _favoriteExecutionTargetIds = [];

        public IReadOnlyList<ExecutionTargetId> FavoriteExecutionTargetIds =>
            _favoriteExecutionTargetIds;

        public void Configure(
            Provider provider,
            ProviderAccount account,
            IReadOnlyList<ExecutionTarget> targets,
            ProviderDiscoverySnapshot snapshot,
            ResourceAccessContext context,
            IReadOnlyList<ExecutionTargetId> favoriteTargetIds)
        {
            _provider = provider;
            _account = account;
            _targets = targets;
            _snapshot = snapshot;
            _favoriteExecutionTargetIds = favoriteTargetIds.ToArray();
        }

        protected override object? Invoke(
            System.Reflection.MethodInfo? targetMethod,
            object?[]? args)
        {
            if (targetMethod is null)
                throw new InvalidOperationException(
                    "The test management proxy received an empty method.");

            return targetMethod.Name switch
            {
                nameof(IHiveManagementFacade.ListProvidersAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<Provider>>.Success(
                            [_provider!])),
                nameof(IHiveManagementFacade.ListProviderAccountsAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<ProviderAccount>>.Success(
                            [_account!])),
                nameof(IHiveManagementFacade.GetFavoriteExecutionTargetIdsAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<ExecutionTargetId>>.Success(
                            _favoriteExecutionTargetIds)),
                nameof(IHiveManagementFacade.ReplaceFavoriteExecutionTargetIdsAsync) =>
                    ReplaceFavoriteExecutionTargetIds(args),
                nameof(IHiveManagementFacade.ListExecutionTargetsAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<ExecutionTarget>>.Success(
                            _targets)),
                nameof(IHiveManagementFacade.GetProviderDiscoveryAsync)
                    when args is { Length: 6 } &&
                         args[0] is ProviderId =>
                    Task.FromResult(
                        Result<ProviderDiscoverySnapshot>.Success(
                            _snapshot!)),
                _ => throw new NotSupportedException(
                    $"The test management proxy does not implement '{targetMethod.Name}'.")
            };
        }
    }

    private sealed class FixedResponseHandler : HttpMessageHandler
    {
        private readonly string _body;

        public FixedResponseHandler(string body)
        {
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _body,
                    Encoding.UTF8,
                    "application/json")
            };

            return Task.FromResult(response);
        }

        private object ReplaceFavoriteExecutionTargetIds(object?[]? args)
        {
            var ids =
                args is not null &&
                args.Length > 0 &&
                args[0] is IReadOnlyList<ExecutionTargetId> favoriteIds
                    ? favoriteIds
                    : Array.Empty<ExecutionTargetId>();

            _favoriteExecutionTargetIds = ids.ToArray();

            return Task.FromResult(
                Result<IReadOnlyList<ExecutionTargetId>>.Success(
                    _favoriteExecutionTargetIds));
        }
    }
}
