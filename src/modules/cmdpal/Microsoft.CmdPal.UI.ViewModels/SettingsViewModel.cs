// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class SettingsViewModel : INotifyPropertyChanged
{
    private readonly SettingsModel _settings;
    private readonly IServiceProvider _serviceProvider;

    public event PropertyChangedEventHandler? PropertyChanged;

    public HotkeySettings? Hotkey
    {
        get => _settings.Hotkey;
        set
        {
            _settings.Hotkey = value ?? SettingsModel.DefaultActivationShortcut;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hotkey)));
            Save();
        }
    }

    public bool UseLowLevelGlobalHotkey
    {
        get => _settings.UseLowLevelGlobalHotkey;
        set
        {
            _settings.UseLowLevelGlobalHotkey = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hotkey)));
            Save();
        }
    }

    public bool AllowExternalReload
    {
        get => _settings.AllowExternalReload;
        set
        {
            _settings.AllowExternalReload = value;
            Save();
        }
    }

    public bool ShowAppDetails
    {
        get => _settings.ShowAppDetails;
        set
        {
            _settings.ShowAppDetails = value;
            Save();
        }
    }

    public bool HotkeyGoesHome
    {
        get => _settings.HotkeyGoesHome;
        set
        {
            _settings.HotkeyGoesHome = value;
            Save();
        }
    }

    public bool BackspaceGoesBack
    {
        get => _settings.BackspaceGoesBack;
        set
        {
            _settings.BackspaceGoesBack = value;
            Save();
        }
    }

    public bool SingleClickActivates
    {
        get => _settings.SingleClickActivates;
        set
        {
            _settings.SingleClickActivates = value;
            Save();
        }
    }

    public bool HighlightSearchOnActivate
    {
        get => _settings.HighlightSearchOnActivate;
        set
        {
            _settings.HighlightSearchOnActivate = value;
            Save();
        }
    }

    public int MonitorPositionIndex
    {
        get => (int)_settings.SummonOn;
        set
        {
            _settings.SummonOn = (MonitorBehavior)value;
            Save();
        }
    }

    public bool ShowSystemTrayIcon
    {
        get => _settings.ShowSystemTrayIcon;
        set
        {
            _settings.ShowSystemTrayIcon = value;
            Save();
        }
    }

    public bool IgnoreShortcutWhenFullscreen
    {
        get => _settings.IgnoreShortcutWhenFullscreen;
        set
        {
            _settings.IgnoreShortcutWhenFullscreen = value;
            Save();
        }
    }

    public bool DisableAnimations
    {
        get => _settings.DisableAnimations;
        set
        {
            _settings.DisableAnimations = value;
            Save();
        }
    }

    public ObservableCollection<ProviderSettingsViewModel> CommandProviders { get; } = [];

    public ObservableCollection<FallbackSettingsViewModel> EnabledFallbackCommands { get; } = [];

    public ObservableCollection<FallbackSettingsViewModel> DisabledFallbackCommands { get; } = [];

    public SettingsViewModel(SettingsModel settings, IServiceProvider serviceProvider, TaskScheduler scheduler)
    {
        _settings = settings;
        _serviceProvider = serviceProvider;

        var activeProviders = GetCommandProviders();

        foreach (var item in activeProviders)
        {
            var providerSettings = settings.GetProviderSettings(item);
            var settingsModel = new ProviderSettingsViewModel(item, providerSettings, _serviceProvider);
            CommandProviders.Add(settingsModel);

            if (item.FallbackItems is not null && item.FallbackItems.Length > 0)
            {
                foreach (var fallback in item.FallbackItems)
                {
                    var id = fallback.IdFromModel;

                    // If the underlying command still has no Id (should be rare now), skip registering until it gets one.
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    if (!providerSettings.FallbackCommands.TryGetValue(id, out var existingSettings))
                    {
                        existingSettings = new FallbackSettings();
                    }

                    var fallbackSettingsModel = new FallbackSettingsViewModel(fallback, existingSettings, settingsModel, _serviceProvider);
                    if (fallbackSettingsModel.IsEnabled)
                    {
                        EnabledFallbackCommands.Add(fallbackSettingsModel);
                    }
                    else
                    {
                        DisabledFallbackCommands.Add(fallbackSettingsModel);
                    }
                }
            }
        }
    }

    private IEnumerable<CommandProviderWrapper> GetCommandProviders()
    {
        var manager = _serviceProvider.GetService<TopLevelCommandManager>()!;
        return manager.CommandProviders;
    }

    // ReorderFallbacks is called after the UI collection has been reordered.
    // Assign descending WeightBoost values (highest priority = largest boost) once,
    // then persist settings.
    public void ReorderFallbacks(FallbackSettingsViewModel droppedCommand, List<FallbackSettingsViewModel> allFallbacks)
    {
        // Highest weight to first item
        var weight = allFallbacks.Count;
        foreach (var f in allFallbacks)
        {
            // Each setter persists WeightBoost on the underlying FallbackSettings object
            f.WeightBoost = weight--;
        }

        Save();
        WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
    }

    private void Save() => SettingsModel.SaveSettings(_settings);
}
