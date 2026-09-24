        var effectiveThemeManager = themeManager ?? ResolveThemeManager(owner);
        using var dialog = new HiveMessageDialog(options, effectiveThemeManager);
        return owner is null
            ? dialog.ShowDialog()
            : dialog.ShowDialog(owner);