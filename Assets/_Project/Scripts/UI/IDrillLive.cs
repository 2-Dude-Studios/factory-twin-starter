namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training areas (M24): a drill that can show its run while it runs, for its area's score board
    /// (TrainingAreaBoard): the checklist, the points and what is due. The version goes up whenever the text changes,
    /// so the board only writes it then.
    /// </summary>
    public interface IDrillLive
    {
        bool Running { get; }
        int LiveVersion { get; }
        string LiveText { get; }
    }
}
