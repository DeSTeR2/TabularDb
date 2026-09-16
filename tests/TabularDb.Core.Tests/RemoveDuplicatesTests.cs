using TabularDb.Core.Operations;
using TabularDb.Core.Types;

namespace TabularDb.Core.Tests;

public class RemoveDuplicatesTests
{
    [Fact]
    public void RemoveDuplicates_TimeNormalized_KeepsFirst()
    {
        var table = TestTables.Create(("Name", StringType.Instance), ("At", TimeType.Instance));
        var first = table.Add("A", "9:05");
        table.Add("A", "09:05:00");
        var third = table.Add("B", "10:00");

        var result = TableOperations.RemoveDuplicates(table);

        Assert.Equal(1, result.RemovedCount);
        Assert.Equal([first.Id, third.Id], table.Rows.Select(r => r.Id));
    }

    [Fact]
    public void RemoveDuplicates_TimeIntervals_ComparedByValue()
    {
        var table = TestTables.Create(("Shift", TimeInvlType.Instance));
        table.Add("09:00-17:30");
        table.Add("9:00:00 - 17:30");
        table.Add("09:00-17:31");

        var result = TableOperations.RemoveDuplicates(table);

        Assert.Equal(1, result.RemovedCount);
        Assert.Equal(2, table.Rows.Count);
    }

    [Fact]
    public void RemoveDuplicates_EmptyTable_RemovesNothing()
    {
        var table = TestTables.Create(("X", IntegerType.Instance));

        Assert.Equal(0, TableOperations.RemoveDuplicates(table).RemovedCount);
    }

    [Fact]
    public void RemoveDuplicates_NoDuplicates_RemovesNothing()
    {
        var table = TestTables.Create(("X", IntegerType.Instance), ("C", CharType.Instance));
        table.Add("1", "a");
        table.Add("1", "A");
        table.Add("2", "a");

        Assert.Equal(0, TableOperations.RemoveDuplicates(table).RemovedCount);
        Assert.Equal(3, table.Rows.Count);
    }

    [Fact]
    public void RemoveDuplicates_NullsAreEqual()
    {
        var table = TestTables.Create(("X", IntegerType.Instance), ("S", StringType.Instance));
        table.Add(null, "x");
        table.Add("", "x");
        table.Add("0", "x");

        var result = TableOperations.RemoveDuplicates(table);

        Assert.Equal(1, result.RemovedCount);
        Assert.Null(table.Rows[0][0]);
        Assert.Equal(0L, table.Rows[1][0]);
    }

    [Theory]
    [InlineData("-0.0", "0")]
    [InlineData("1e3", "1000")]
    [InlineData("0.50", ".5")]
    public void RemoveDuplicates_RealsComparedByValue(string a, string b)
    {
        var table = TestTables.Create(("R", RealType.Instance));
        table.Add(a);
        table.Add(b);

        Assert.Equal(1, TableOperations.RemoveDuplicates(table).RemovedCount);
    }

    [Fact]
    public void RemoveDuplicates_DryRun_DoesNotChangeTable()
    {
        var table = TestTables.Create(("X", IntegerType.Instance));
        table.Add("1");
        var duplicate = table.Add("1");

        var result = TableOperations.RemoveDuplicates(table, dryRun: true);

        Assert.Equal(1, result.RemovedCount);
        Assert.Equal(duplicate.Id, Assert.Single(result.RemovedRows).Id);
        Assert.Equal(2, table.Rows.Count);
    }

    [Fact]
    public void RemoveDuplicates_ManyCopies_KeepsOrderOfFirstOccurrences()
    {
        var table = TestTables.Create(("X", IntegerType.Instance));
        foreach (var v in new[] { "3", "1", "3", "2", "1", "3" })
            table.Add(v);

        TableOperations.RemoveDuplicates(table);

        Assert.Equal([3L, 1L, 2L], table.Rows.Select(r => (long)r[0]!));
    }
}
