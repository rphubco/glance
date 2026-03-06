namespace Glance.Models;

using Glance.UI.Windows;
using System;

public record NeighborInfo(string ProfileId, long Version, int NameplateIcon = 0);
public record CachedProfile(int? Id, long Version, ProfileData? Data, DateTime FetchedAt, bool Unverified = false);
