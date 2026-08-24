namespace Overmem.Abstractions.Search;

public enum ValueSearchComparison
{
    Exact = 0,
    Changed = 1,
    Unchanged = 2,
    Increased = 3,
    Decreased = 4,
    NotEqual = 5,
    Between = 6,
    IncreasedBy = 7,
    DecreasedBy = 8,
    ChangedBy = 9,
}