using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApp.ViewModels;

public partial class LaboratoryViewModel : ObservableObject
{
    public TablePager<LabOrderRecord> PendingPager { get; } = new(SampleData.PendingLabOrders, "laboratory order", "pending laboratory orders");
    public TablePager<LabResultRecord> CompletedPager { get; } = new(SampleData.CompletedLabResults, "laboratory result", "completed laboratory results");
    public IReadOnlyList<LabOrderRecord> PendingOrders => PendingPager.Items;
    public IReadOnlyList<LabResultRecord> CompletedResults => CompletedPager.Items;
}
