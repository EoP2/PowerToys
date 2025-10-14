// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class FallbackCommandItem : CommandItem, IFallbackCommandItem, IFallbackHandler
{
    private IFallbackHandler? _fallbackHandler;

    /// <summary>
    /// Gets a convenience Id surface for callers (mirrors the underlying Command.Id).
    /// Always non-null (empty string if Command is null, though constructor enforces initial presence).
    /// </summary>
    public string Id => Command?.Id ?? string.Empty;

    public FallbackCommandItem(ICommand command, string displayTitle)
        : base(command)
    {
        // Enforce that fallback commands have a stable Id for hotkeys / aliases / persistence.
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Id))
        {
            throw new ArgumentException("FallbackCommandItem requires the provided ICommand to have a non-empty Id. Set command.Id before constructing the fallback item.", nameof(command));
        }

        DisplayTitle = displayTitle;
        if (command is IFallbackHandler f)
        {
            _fallbackHandler = f;
        }
    }

    public IFallbackHandler? FallbackHandler
    {
        get => _fallbackHandler ?? this;
        init => _fallbackHandler = value;
    }

    public virtual string DisplayTitle { get; }

    public virtual void UpdateQuery(string query) => _fallbackHandler?.UpdateQuery(query);
}
