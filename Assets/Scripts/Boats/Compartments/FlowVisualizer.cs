public static class FlowVisualizer
{
    public static void ReportAcceptedFlow(
        CompartmentConnection conn,
        Compartment source,
        Compartment target,
        float flow)
    {
        if (conn.flowVisual == null)
            return;

        conn.flowVisual.SetFlow(source, target, flow);
    }
}
