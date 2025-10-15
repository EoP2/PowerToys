// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class FallbackSettingsViewModel(
    TopLevelViewModel fallback,
    FallbackSettings _fallbackSettings,
    CommandProviderWrapper _provider,
    ProviderSettingsViewModel _providerSettings,
    IServiceProvider _serviceProvider) : ObservableObject
{
    private readonly SettingsModel _settings = _serviceProvider.GetService<SettingsModel>()!;

    public string DisplayName => fallback.DisplayTitle;

    public string Subtitle => fallback.Subtitle;

    public string ExtensionName => _providerSettings.ExtensionName;

    public IExtensionWrapper? Extension => _providerSettings.Extension;

    public string ExtensionVersion => _providerSettings.ExtensionVersion;

    public IconInfoViewModel Icon => _providerSettings.Icon;

    public bool HasSettings => _providerSettings.HasSettings;

    public ContentPageViewModel? SettingsPage => _providerSettings.SettingsPage;

    [ObservableProperty]
    public partial bool LoadingSettings { get; set; } = _providerSettings.LoadingSettings;

    public bool IsEnabled
    {
        get => _fallbackSettings.IsEnabled;
        set
        {
            if (value != _fallbackSettings.IsEnabled)
            {
                _fallbackSettings.IsEnabled = value;
                Save();
                WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
                OnPropertyChanged(nameof(IsEnabled));
            }

            if (value == true)
            {
                _provider.CommandsChanged -= Provider_CommandsChanged;
                _provider.CommandsChanged += Provider_CommandsChanged;
            }
        }
    }

    private void Save() => SettingsModel.SaveSettings(_settings);

    private void Provider_CommandsChanged(CommandProviderWrapper sender, CommandPalette.Extensions.IItemsChangedEventArgs args)
    {
        OnPropertyChanged(nameof(_providerSettings.ExtensionSubtext));
        OnPropertyChanged(nameof(_providerSettings.FallbackCommands));
    }
}
