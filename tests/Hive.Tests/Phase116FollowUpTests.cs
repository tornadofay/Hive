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

        var result = await adapter.ListModelsAsync(
            new Uri("https://openrouter.ai/api/v1/models"),
            OpenAICompatibleModelCatalogFormat.OpenRouter);

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
        var inputPrice = model.Pricing!.Prices.Single(
            price => price.BillingUnit == "input_token");
        var outputPrice = model.Pricing.Prices.Single(
            price => price.BillingUnit == "output_token");

        Assert.Equal(0.00003m, inputPrice.Price);
        Assert.Equal(0.00006m, outputPrice.Price);
        Assert.Equal("USD", inputPrice.Currency);
        Assert.Equal("USD", outputPrice.Currency);

        Assert.NotNull(model.ExtensionData);
        Assert.True(model.ExtensionData!.ContainsKey("architecture"));
        Assert.True(model.ExtensionData.ContainsKey("supported_parameters"));
    }

    [Fact]
    public async Task OpenAICompatibleAdapter_RecognizesOpenRouterZeroPricingWithoutCurrency()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "free-model",
                      "name": "Free Model",
                      "pricing": {
                        "prompt": "0",
                        "completion": "0"
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://openrouter.ai/api/v1/")));

        var result = await adapter.ListModelsAsync(
            new Uri("https://openrouter.ai/api/v1/models"),
            OpenAICompatibleModelCatalogFormat.OpenRouter);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var model = Assert.Single(result.Value!.Models);
        Assert.NotNull(model.Pricing);
        Assert.True(model.Pricing!.ExplicitFreeEvidence);

        Assert.All(
            model.Pricing.Prices,
            price =>
            {
                Assert.Equal("USD", price.Currency);
                Assert.Equal(0m, price.Price);
            });
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
                "Execution Targets"
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
        var fixture = CreateFixture(favoriteFirstModel: true);

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

        var actionLayout = Assert.IsType<TableLayoutPanel>(
            view.CrudPage.ActionBarPanel.Controls[0]);
        var visibleActionButtons = Assert.IsType<FlowLayoutPanel>(
            actionLayout.Controls
                .Cast<Control>()
                .Single(control =>
                    control is FlowLayoutPanel &&
                    control.Controls.OfType<HiveButton>().Any()))
            .Controls
            .OfType<HiveButton>()
            .Where(button => button.Visible)
            .ToArray();

        Assert.Single(visibleActionButtons);
        Assert.Equal("Add to Favorites", visibleActionButtons[0].Text);
        Assert.True(visibleActionButtons[0].Width >= 120);

        var pageRoot = Assert.IsType<TableLayoutPanel>(view.Controls[0]);
        Assert.Same(view.CrudPage.HeaderPanel, pageRoot.Controls[0]);

        var initialDetailsWidth = view.DetailsPanelWidth;
        Assert.InRange(initialDetailsWidth, 390, 410);
        host.ClientSize = new Size(1320, 760);
        host.PerformLayout();
        view.PerformLayout();
        Application.DoEvents();
        Assert.Equal(initialDetailsWidth, view.DetailsPanelWidth);

        Assert.Equal(
            [
                "Model",
                "Price / 1M",
                "Context",
                "Text",
                "Vision",
                "Tools",
                "Reasoning"
            ],
            view.CrudPage.Columns.Select(column => column.Header));

        Assert.Equal(2, view.ModelsList.Items.Count);
        Assert.Equal(220, view.CrudPage.SearchMaximumWidth);
        var filterSurfaceText = CollectVisibleControlText(view);
        Assert.Contains("Show models without comparable pricing", filterSurfaceText);
        Assert.Contains("Show models above the price range", filterSurfaceText);
        Assert.StartsWith(
            "★ ",
            view.ModelsList.Items[0].Text);
        Assert.Contains(
            "rich-model",
            view.ModelsList.Items[0].Text);
        // Column order: 0 Model, 1 Price / 1M, 2 Context, 3 Text, 4 Vision,
        // 5 Tools, 6 Reasoning. Price and Context were added because they are
        // the decision-relevant facts when choosing a model to bring into Hive.
        Assert.Equal("✓", view.ModelsList.Items[0].SubItems[3].Text);
        Assert.Equal("✓", view.ModelsList.Items[0].SubItems[4].Text);
        Assert.Equal("✓", view.ModelsList.Items[0].SubItems[5].Text);
        Assert.Equal("✓", view.ModelsList.Items[0].SubItems[6].Text);

        // The rich model's comparable rate is its highest token rate, $5.00 / 1M.
        Assert.StartsWith("$5", view.ModelsList.Items[0].SubItems[1].Text);

        // The second model reports vision unsupported and no context window.
        Assert.Equal("✕", view.ModelsList.Items[1].SubItems[4].Text);
        Assert.Equal("—", view.ModelsList.Items[1].SubItems[2].Text);

        view.ModelsList.Items[0].Selected = true;
        view.ModelsList.Items[0].Focused = true;
        Application.DoEvents();

        var detailsText = CollectVisibleControlText(view.DetailsContent);

        Assert.Contains("rich-model", detailsText);
        Assert.Contains("text, image, audio", detailsText);
        Assert.Equal(
            ["Overview", "Details", "Technical"],
            view.DetailsTabs.TabPages.Cast<TabPage>().Select(page => page.Text));
        Assert.NotNull(view.DetailsTabs.SelectedTab);
        Assert.Equal("Overview", view.DetailsTabs.SelectedTab!.Text);
        Assert.NotEmpty(view.OverviewDetailsTab.Controls);
        Assert.Empty(view.ModelDetailsTab.Controls);
        Assert.Empty(view.TechnicalDetailsTab.Controls);

        Assert.Contains("Capabilities", detailsText);
        Assert.Contains("Text: Supported", detailsText);
        Assert.Contains(
            "Text: Supported" + Environment.NewLine +
            "Vision: Supported" + Environment.NewLine +
            "Tools: Supported",
            detailsText);
        Assert.Contains("Structured: Supported", detailsText);
        Assert.DoesNotContain("✕", detailsText);
        Assert.DoesNotContain("✓", detailsText);
        Assert.Contains("Price / 1M", detailsText);
        Assert.Contains("5.00", detailsText);
        Assert.DoesNotContain("deterministic-fixture", detailsText);

        view.DetailsTabs.SelectedIndex = 1;
        Application.DoEvents();

        var modelDetailsText = CollectVisibleControlText(view.ModelDetailsTab);
        Assert.NotEmpty(view.ModelDetailsTab.Controls);
        Assert.Contains("ID: rich-model", modelDetailsText);
        Assert.Contains("example-family", modelDetailsText);
        Assert.Contains("Reasoning: Supported", modelDetailsText);
        Assert.Contains("medium", modelDetailsText);
        Assert.Contains("Limits", modelDetailsText);
        Assert.Contains("131,072 tokens", modelDetailsText);
        Assert.Empty(view.TechnicalDetailsTab.Controls);

        view.DetailsTabs.SelectedIndex = 2;
        Application.DoEvents();

        var technicalText = CollectVisibleControlText(view.TechnicalDetailsTab);
        Assert.NotEmpty(view.TechnicalDetailsTab.Controls);
        Assert.Contains("Pricing evidence", technicalText);
        Assert.Contains("Input tokens", technicalText);
        Assert.Contains("Operational", technicalText);
        Assert.Contains("deterministic-fixture", technicalText);

        var detailControls = view.DetailsContent.Controls
            .Cast<Control>()
            .ToArray();

        Assert.Equal(3, detailControls.Length);
        Assert.Equal(
            detailControls.Length,
            view.DetailsContent.Controls.Cast<Control>().Count());
        Assert.Contains(
            detailControls,
            control => control is HiveTabControl);
    }

    [WinFormsFact]
    public async Task ModelInformationView_DisplaysTieredPricingDetails()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        var fixture = CreateFixture(richModelUsesTieredPricing: true);

        using var host = new Form { Size = new Size(1160, 760) };
        using var view = new HiveModelInformationSettingsView(
            fixture.Management,
            fixture.Context,
            themeManager);

        host.Controls.Add(view);
        host.Show();
        Application.DoEvents();

        await view.InitializeAsync();
        Application.DoEvents();

        view.ModelsList.Items[0].Selected = true;
        view.ModelsList.Items[0].Focused = true;
        Application.DoEvents();

        view.DetailsTabs.SelectedIndex = 2;
        Application.DoEvents();

        var detailsText = CollectVisibleControlText(view.TechnicalDetailsTab);

        Assert.Contains("Tiered rates:", detailsText);
        Assert.Contains("long-context", detailsText);
        Assert.Contains("minimum prompt tokens=272000", detailsText);
        Assert.Contains("Input tokens: 4 USD per 1M units", detailsText);
        Assert.Contains("Output tokens: 15 USD per 1M units", detailsText);
    }

    [WinFormsFact]
    public async Task ModelInformationView_ShowsVerticalScrollForLongTechnicalDetailsAtNormalSize()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        var fixture = CreateFixture(richModelUsesLongProviderEvidence: true);

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

        view.ModelsList.Items[0].Selected = true;
        view.ModelsList.Items[0].Focused = true;
        view.DetailsTabs.SelectedIndex = 2;
        Application.DoEvents();

        Assert.True(view.DetailsScrollHost.VerticalScrollState.CanScroll);
        Assert.True(view.DetailsScrollHost.VerticalScrollBarForTesting.Visible);
    }

    [WinFormsFact]
    public async Task ModelInformationView_FilterAreaReservesSpaceForAllControlsAtNormalSize()
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

        var pageRoot = Assert.IsType<TableLayoutPanel>(view.Controls[0]);
        var contextAndFilters = Assert.IsType<TableLayoutPanel>(
            pageRoot.GetControlFromPosition(0, 1));
        var mainSplit = Assert.IsType<SplitContainer>(
            pageRoot.GetControlFromPosition(0, 2));

        Assert.True(view.ShowUnpricedModelsFilter.Visible);
        Assert.True(view.ShowAboveRangeModelsFilter.Visible);
        Assert.True(view.CapabilityFilter.Visible);
        Assert.True(view.CapabilityStateFilter.Visible);

        var mainSplitTop = mainSplit.PointToScreen(Point.Empty).Y;

        foreach (var control in new Control[]
        {
            view.CapabilityFilter,
            view.CapabilityStateFilter,
            view.ShowUnpricedModelsFilter,
            view.ShowAboveRangeModelsFilter
        })
        {
            var bottom = control.PointToScreen(
                new Point(0, control.ClientSize.Height)).Y;
            Assert.True(
                bottom <= mainSplitTop,
                $"{control.AccessibleName} extends into the CRUD/details surface: bottom={bottom}, mainSplitTop={mainSplitTop}.");
        }

        Assert.True(
            mainSplit.Top >= contextAndFilters.Bottom,
            $"CRUD/details surface overlaps the context/filter container: mainSplitTop={mainSplit.Top}, contextAndFiltersBottom={contextAndFilters.Bottom}.");
        Assert.True(contextAndFilters.Height > 72);
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

        string? confirmedModelName = null;
        view.FavoriteConfirmationOverride = modelName =>
        {
            confirmedModelName = modelName;
            return true;
        };

        var actionLayout = Assert.IsType<TableLayoutPanel>(
            view.CrudPage.ActionBarPanel.Controls[0]);
        var actionButtons = Assert.IsType<FlowLayoutPanel>(
            actionLayout.Controls
                .Cast<Control>()
                .Single(control =>
                    control is FlowLayoutPanel &&
                    control.Controls.OfType<HiveButton>().Any()));
        var addButton = actionButtons.Controls
            .OfType<HiveButton>()
            .Single(button => button.Text == "Add to Favorites");

        addButton.PerformClick();
        Application.DoEvents();

        Assert.Equal("rich-model", confirmedModelName);
        Assert.Contains(fixture.Target.Id, fixture.ManagementProxy.FavoriteExecutionTargetIds);
        Assert.StartsWith("★ ", view.ModelsList.Items[0].Text);
    }

    [WinFormsFact]
    public async Task ModelInformationView_MaxZeroIncludesExplicitFreeEvidenceEvenWithPaidComparableBaseRates()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        var fixture = CreateFixture(
            secondModelFree: true,
            secondModelHasPaidComparableBaseRates: true);

        using var host = new Form { Size = new Size(1160, 760) };
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

        view.MaxPriceFilter.Value = 0;
        Application.DoEvents();

        Assert.Single(view.ModelsList.Items);
        Assert.Equal("second-model", view.ModelsList.Items[0].Text);
    }

    [WinFormsFact]
    public async Task ModelInformationView_PriceRangeFilterCanShowOnlyFreeModels()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        var fixture = CreateFixture(secondModelFree: true);

        using var host = new Form { Size = new Size(1160, 760) };
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
        Assert.Equal(0, view.MinPriceFilter.Value);

        // The ceiling is derived from the snapshot's price distribution, so
        // assert the filter starts at its own ceiling rather than a fixed value.
        Assert.Equal(view.MaxPriceFilter.Maximum, view.MaxPriceFilter.Value);

        view.MaxPriceFilter.Value = 0;
        Application.DoEvents();

        Assert.Single(view.ModelsList.Items);
        Assert.Equal("second-model", view.ModelsList.Items[0].Text);
    }

    [WinFormsFact]
    public async Task ModelInformationView_PriceRangeFilterNormalizesPerTokenPricing()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        var fixture = CreateFixture(
            secondModelFree: true,
            richModelUsesPerTokenPricing: true);

        using var host = new Form { Size = new Size(1160, 760) };
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

        // The fixture reports $0.00000070 per output token, which is $0.70/M.
        view.MaxPriceFilter.Value = 70;
        Application.DoEvents();
        Assert.Equal(2, view.ModelsList.Items.Count);

        // $0.69/M must exclude the $0.70/M rich model.
        view.MaxPriceFilter.Value = 69;
        Application.DoEvents();
        Assert.Single(view.ModelsList.Items);
        Assert.Equal("second-model", view.ModelsList.Items[0].Text);

        view.MaxPriceFilter.Value = 0;
        Application.DoEvents();
        Assert.Single(view.ModelsList.Items);
        Assert.Equal("second-model", view.ModelsList.Items[0].Text);
    }

    [WinFormsFact]
    public async Task ModelInformationView_DoesNotAssumeMissingPricingQuantity()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        var fixture = CreateFixture(
            secondModelFree: true,
            richModelUsesUnknownPricing: true);

        using var host = new Form { Size = new Size(1160, 760) };
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

        // The rich model has token-like numeric prices but no established source quantity.
        // It must not be interpreted as a $0.70/M comparable price.
        //
        // The slider ceiling is now derived from the data instead of a fixed $5,
        // so a bounded selection is expressed relative to that ceiling. Only the
        // free model has a comparable rate here, giving a $1.00 ceiling, so
        // $0.50 is a bounded maximum that must exclude the unknown-pricing model.
        view.MaxPriceFilter.Value = 50;
        Application.DoEvents();

        Assert.Single(view.ModelsList.Items);
        Assert.Equal("second-model", view.ModelsList.Items[0].Text);
    }

    [WinFormsFact]
    public async Task ModelInformationView_CapabilityFilterMatchesState()
    {
        var fixture = CreateFixture();
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var host = new Form { Size = new Size(1160, 760) };
        using var view = new HiveModelInformationSettingsView(
            fixture.Management,
            fixture.Context,
            themeManager);

        host.Controls.Add(view);
        host.Show();
        Application.DoEvents();

        await view.InitializeAsync();
        Application.DoEvents();

        var visionIndex = Enumerable.Range(0, view.CapabilityFilter.Items.Count)
            .Single(index => string.Equals(
                view.CapabilityFilter.Items[index]?.ToString(),
                "Vision",
                StringComparison.Ordinal));

        var supportedIndex = Enumerable.Range(0, view.CapabilityStateFilter.Items.Count)
            .Single(index => string.Equals(
                view.CapabilityStateFilter.Items[index]?.ToString(),
                "Supported",
                StringComparison.Ordinal));

        view.CapabilityFilter.SelectedIndex = visionIndex;
        view.CapabilityStateFilter.SelectedIndex = supportedIndex;
        Application.DoEvents();

        Assert.Single(view.ModelsList.Items);
        Assert.Contains("rich-model", view.ModelsList.Items[0].Text);
    }

    [WinFormsFact]
    public async Task ModelInformationView_CapabilityFilterUsesSupportedState()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        var fixture = CreateFixture();

        using var host = new Form { Size = new Size(1160, 760) };
        using var view = new HiveModelInformationSettingsView(
            fixture.Management,
            fixture.Context,
            themeManager);

        host.Controls.Add(view);
        host.Show();
        Application.DoEvents();

        await view.InitializeAsync();
        Application.DoEvents();

        var visionIndex = Enumerable.Range(0, view.CapabilityFilter.Items.Count)
            .Single(index =>
                string.Equals(
                    view.CapabilityFilter.Items[index]?.ToString(),
                    "Vision",
                    StringComparison.Ordinal));

        var trueIndex = Enumerable.Range(0, view.CapabilityStateFilter.Items.Count)
            .Single(index =>
                string.Equals(
                    view.CapabilityStateFilter.Items[index]?.ToString(),
                    "Supported",
                    StringComparison.Ordinal));

        view.CapabilityFilter.SelectedIndex = visionIndex;
        view.CapabilityStateFilter.SelectedIndex = trueIndex;
        Application.DoEvents();

        Assert.Single(view.ModelsList.Items);
        Assert.Contains("rich-model", view.ModelsList.Items[0].Text);
    }

    [WinFormsFact]
    public async Task ModelInformationView_DoesNotAddFavoriteWhenConfirmationIsDeclined()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        var fixture = CreateFixture();

        using var host = new Form { Size = new Size(1160, 760) };
        using var view = new HiveModelInformationSettingsView(
            fixture.Management,
            fixture.Context,
            themeManager);

        host.Controls.Add(view);
        host.Show();
        Application.DoEvents();

        await view.InitializeAsync();
        Application.DoEvents();

        view.ModelsList.Items[0].Selected = true;
        view.FavoriteConfirmationOverride = _ => false;

        var actionLayout = Assert.IsType<TableLayoutPanel>(
            view.CrudPage.ActionBarPanel.Controls[0]);
        var actionButton = actionLayout.Controls
            .OfType<FlowLayoutPanel>()
            .SelectMany(panel => panel.Controls.OfType<HiveButton>())
            .Single(button => button.Text == "Add to Favorites");

        actionButton.PerformClick();
        Application.DoEvents();

        Assert.DoesNotContain(
            fixture.Target.Id,
            fixture.ManagementProxy.FavoriteExecutionTargetIds);
        Assert.DoesNotContain("★ ", view.ModelsList.Items[0].Text);
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

        var detailControlsBeforeSelection = view.DetailsContent.Controls
            .Cast<Control>()
            .ToArray();

        firstItem.Selected = false;
        secondItem.Selected = true;
        secondItem.Focused = true;
        Application.DoEvents();

        view.DetailsTabs.SelectedIndex = 0;
        Application.DoEvents();
        var overviewText = CollectVisibleControlText(view.OverviewDetailsTab);
        Assert.Contains("second-model", CollectVisibleControlText(view.DetailsContent));
        Assert.Contains("Capabilities", overviewText);
        Assert.Contains("Vision: Unsupported", overviewText);
        Assert.DoesNotContain("rich-model", overviewText);

        view.DetailsTabs.SelectedIndex = 1;
        Application.DoEvents();
        var detailsText = CollectVisibleControlText(view.ModelDetailsTab);
        Assert.DoesNotContain("second-provider", detailsText); // provider evidence belongs to Technical

        Assert.Contains("Reasoning: Unknown / unreported", detailsText);
        Assert.DoesNotContain("rich-model", detailsText);

        view.DetailsTabs.SelectedIndex = 2;
        Application.DoEvents();
        var technicalText = CollectVisibleControlText(view.TechnicalDetailsTab);
        Assert.Contains("second-provider", technicalText);
        Assert.DoesNotContain("rich-model", technicalText);

        Assert.DoesNotContain("✓", CollectVisibleControlText(view.DetailsContent));
        Assert.DoesNotContain("✕", CollectVisibleControlText(view.DetailsContent));
        Assert.Equal(
            detailControlsBeforeSelection,
            view.DetailsContent.Controls.Cast<Control>().ToArray());
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
        bool favoriteFirstModel = false,
        bool secondModelFree = false,
        bool richModelUsesPerTokenPricing = false,
        bool richModelUsesUnknownPricing = false,
        bool secondModelHasPaidComparableBaseRates = false,
        bool richModelUsesTieredPricing = false,
        bool richModelUsesLongProviderEvidence = false)
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
                richModelUsesPerTokenPricing
                    ? [
                        new ProviderModelPrice(
                            "input_token",
                            0.00000035m,
                            "USD",
                            1m),
                        new ProviderModelPrice(
                            "output_token",
                            0.00000070m,
                            "USD",
                            1m)
                    ]
                    : richModelUsesUnknownPricing
                        ? [
                            new ProviderModelPrice(
                                "input_token",
                                0.00000035m,
                                "USD"),
                            new ProviderModelPrice(
                                "output_token",
                                0.00000070m,
                                "USD")
                        ]
                        : [
                            new ProviderModelPrice(
                                "input_token",
                                1.25m,
                                "USD",
                                1_000_000m),
                            new ProviderModelPrice(
                                "output_token",
                                5m,
                                "USD",
                                1_000_000m)
                        ],
                variants: richModelUsesTieredPricing
                    ? [
                        new ProviderModelPricingVariant(
                            "long-context",
                            [
                                new ProviderModelPrice(
                                    "input_token",
                                    4m,
                                    "USD",
                                    1_000_000m),
                                new ProviderModelPrice(
                                    "output_token",
                                    15m,
                                    "USD",
                                    1_000_000m)
                            ],
                            new Dictionary<string, string>
                            {
                                ["min_prompt_tokens"] = "272000"
                            })
                    ]
                    : null),
            extensionData: new Dictionary<string, JsonElement>
            {
                ["vendor_library"] =
                    JsonSerializer.SerializeToElement(
                        richModelUsesLongProviderEvidence
                            ? "deterministic-fixture-" + new string('x', 4000)
                            : "deterministic-fixture")
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
                    HiveCapabilityKeys.Vision,
                    CapabilityState.Unsupported),
                new CapabilityStateEntry(
                    HiveCapabilityKeys.Reasoning,
                    CapabilityState.Unknown)
            ],
            ["text"],
            ["text"],
            family: "second-family",
            modelType: "chat",
            operationalState: "active",
            pricing: new ProviderModelPricing(
                secondModelFree && !secondModelHasPaidComparableBaseRates
                    ? [
                        new ProviderModelPrice(
                            "input_token",
                            0m,
                            "USD",
                            1_000_000m),
                        new ProviderModelPrice(
                            "output_token",
                            0m,
                            "USD",
                            1_000_000m)
                    ]
                    : [
                        new ProviderModelPrice(
                            "input_token",
                            0.25m,
                            "USD",
                            1_000_000m),
                        new ProviderModelPrice(
                            "output_token",
                            0.5m,
                            "USD",
                            1_000_000m)
                    ],
                explicitFreeEvidence: secondModelFree));

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

    }
}
