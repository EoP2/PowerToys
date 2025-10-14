// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class FallbackSettings
{
    public bool IsEnabled { get; set; } = true;

    public int WeightBoost { get; set; }

    public bool IncludeInGlobalResults { get; set; }

    public FallbackSettings()
    {
    }

    public FallbackSettings(bool isEnabled, int weightBoost, bool includeInGlobalResults)
    {
        IsEnabled = isEnabled;
        WeightBoost = weightBoost;
        IncludeInGlobalResults = includeInGlobalResults;
    }
}
