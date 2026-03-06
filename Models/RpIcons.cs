namespace Glance.Models;

public static class RpIcons
{
    public record IconEntry(int Id, string Label);

    public static readonly IconEntry[] CommunityIcons =
    {
        new(66452, "Leveling"),
        new(66453, "Casual"),
        new(66454, "Hardcore"),
        new(66455, "Battle"),
        new(66456, "Crafting"),
        new(66457, "Gathering"),
        new(66458, "Housing"),
        new(66459, "Hunts"),
        new(66460, "Treasure Hunt"),
        new(66461, "PvP"),
        new(66462, "Fishing"),
        new(66463, "Doman Mahjong"),
        new(66464, "Performance"),
        new(66465, "Making Friends"),
        new(66466, "Novice Support"),
        new(66467, "Role-playing"),
        new(66468, "Player Events"),
        new(66469, "Glamours"),
        new(66470, "Group Pose"),
        new(66471, "Triple Triad"),
        new(66472, "Collectables"),
        new(66473, "Chatting"),
    };

    public static readonly IconEntry[] HousingIcons =
    {
        new(66410, "Venue"),
        new(66411, "Café"),
        new(66412, "Florist"),
        new(66413, "Photo Studio"),
        new(66414, "Library"),
        new(66415, "Haunted House"),
        new(66416, "Atelier"),
        new(66417, "Bathhouse"),
        new(66418, "Garden"),
        new(66419, "Far Eastern"),
        new(66420, "Visitors Welcome"),
        new(66421, "Bakery"),
        new(66422, "Under Renovation"),
        new(66423, "Concert Hall"),
        new(66401, "Emporium"),
        new(66402, "Boutique"),
        new(66403, "Designer Home"),
        new(66404, "Message Book"),
        new(66405, "Tavern"),
        new(66406, "Eatery"),
        new(66407, "Immersive Experience"),
        new(66408, "Aquarium"),
        new(66409, "Sanctum"),
    };

    public static IconEntry? Find(int id)
    {
        foreach (var e in CommunityIcons) if (e.Id == id) return e;
        foreach (var e in HousingIcons) if (e.Id == id) return e;
        return null;
    }
}
