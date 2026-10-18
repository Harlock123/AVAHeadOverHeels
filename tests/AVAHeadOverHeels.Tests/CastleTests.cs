using AVAHeadOverHeels.Engine;

namespace AVAHeadOverHeels.Tests;

public class CastleTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void Every_room_can_be_reached_and_doors_match_on_both_sides(int level)
    {
        var c = CastleBook.Build(level);
        Assert.All(c.Distances(0), d => Assert.True(d >= 0));
        foreach (var r in c.Rooms)
            for (int s = 0; s < 4; s++)
                if (r.Doors[s])
                {
                    int n = c.Through(r.Index, (Side)s);
                    Assert.True(n >= 0);
                    Assert.True(c.Rooms[n].Doors[(int)RoomDef.Opposite((Side)s)]);
                }
    }

    [Fact]
    public void Head_heels_and_the_crown_are_in_three_different_rooms_well_apart()
    {
        for (int level = 0; level < CastleBook.Count; level++)
        {
            var c = CastleBook.Build(level);
            Assert.Equal(3, new[] { c.HeelsStart, c.HeadStart, c.CrownRoom }.Distinct().Count());
            Assert.True(c.Distances(c.HeelsStart)[c.HeadStart] >= 2);
            Assert.True(c.Rooms[c.HeadStart].Island);
            Assert.True(c.Rooms[c.CrownRoom].Crown);
        }
    }

    [Fact]
    public void Every_other_room_can_be_crossed_by_head_by_heels_and_by_both()
    {
        var c = CastleBook.Build(3);
        foreach (var r in c.Rooms.Where(r => !r.Island))
            foreach (var a in Ability.All)
                Assert.True(CastleBook.Proven(r, a), $"room {r.Index} by {a.Name}");
    }

    /// <summary>Where each way of getting about can get to in an island room, starting from a door.</summary>
    private static HashSet<Spot> From(RoomDef r, Ability a, Spot s) => Graph.Build(r, a).Reach(s);

    [Fact]
    public void Head_cannot_leave_the_island_heels_can_get_onto_it_and_only_both_together_reach_the_crown()
    {
        var c = CastleBook.Build(0);
        var island = c.Rooms[c.HeadStart];
        int door = Enumerable.Range(0, 4).First(s => island.Doors[s]);
        var (dx, dy) = RoomDef.DoorCell((Side)door);
        var atDoor = new Spot(dx, dy, 0);
        var middle = new Spot(Field.Mid, Field.Mid, 0);
        Assert.DoesNotContain(atDoor, From(island, Ability.Head, middle));
        Assert.Contains(middle, From(island, Ability.Heels, atDoor));
        Assert.Contains(atDoor, From(island, Ability.Joined, middle));

        var crown = c.Rooms[c.CrownRoom];
        door = Enumerable.Range(0, 4).First(s => crown.Doors[s]);
        (dx, dy) = RoomDef.DoorCell((Side)door);
        atDoor = new Spot(dx, dy, 0);
        var top = new Spot(Field.Mid, Field.Mid, 2);
        Assert.DoesNotContain(top, From(crown, Ability.Head, atDoor));
        Assert.DoesNotContain(top, From(crown, Ability.Heels, atDoor));
        Assert.Contains(top, From(crown, Ability.Joined, atDoor));
    }

    [Fact]
    public void The_same_castle_every_time()
    {
        var a = CastleBook.Build(1);
        var b = CastleBook.Build(1);
        Assert.Equal(a.HeadStart, b.HeadStart);
        Assert.Equal(a.CrownRoom, b.CrownRoom);
        Assert.Equal(a.Rooms.Select(r => string.Join(",", r.Doors)), b.Rooms.Select(r => string.Join(",", r.Doors)));
    }
}
