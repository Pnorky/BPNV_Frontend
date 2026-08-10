using System.Collections.Generic;

namespace AvaloniaApp;

public class NavItem
{
    public string Icon { get; set; } = "";
    public string Text { get; set; } = "";
    public string Tag { get; set; } = "";
    public bool IsChild { get; set; }
}

public class PatientRecord
{
    public string PatientId { get; set; } = "";
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string Age { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Status { get; set; } = "";
}

public class InvoiceRecord
{
    public string InvoiceNo { get; set; } = "";
    public string Patient { get; set; } = "";
    public string Service { get; set; } = "";
    public string Amount { get; set; } = "";
    public string Insurance { get; set; } = "";
    public string Status { get; set; } = "";
}

public class MedicineRecord
{
    public string MedicineCode { get; set; } = "";
    public string MedicineName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Stock { get; set; } = "";
    public string UnitPrice { get; set; } = "";
    public string ExpiryDate { get; set; } = "";
    public string Status { get; set; } = "";
}

public class LabOrderRecord
{
    public string OrderNo { get; set; } = "";
    public string Patient { get; set; } = "";
    public string TestType { get; set; } = "";
    public string OrderedBy { get; set; } = "";
    public string DateOrdered { get; set; } = "";
    public string Priority { get; set; } = "";
}

public class LabResultRecord
{
    public string OrderNo { get; set; } = "";
    public string Patient { get; set; } = "";
    public string TestType { get; set; } = "";
    public string Result { get; set; } = "";
    public string Completed { get; set; } = "";
}

public class RadiologyRecord
{
    public string OrderNo { get; set; } = "";
    public string Patient { get; set; } = "";
    public string Procedure { get; set; } = "";
    public string OrderedBy { get; set; } = "";
    public string Schedule { get; set; } = "";
    public string Status { get; set; } = "";
}

public class MedicalRecord
{
    public string MRN { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string LastVisit { get; set; } = "";
    public string RecordStatus { get; set; } = "";
    public string ChartComplete { get; set; } = "";
    public string Location { get; set; } = "";
}

public class StaffRecord
{
    public string StaffId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Department { get; set; } = "";
    public string Role { get; set; } = "";
    public string Status { get; set; } = "";
}

public static class SampleData
{
    public static List<NavItem> NavItems => new()
    {
        new() { Icon = "LayoutDashboard", Text = "Overview", Tag = "Dashboard" },
        new() { Icon = "ShoppingBasket", Text = "New Sale", Tag = "Sales" },
        new() { Icon = "Boxes", Text = "Inventory", Tag = "InventoryProducts" },
        new() { Icon = "Package", Text = "Products", Tag = "InventoryProducts", IsChild = true },
        new() { Icon = "Package", Text = "Add Product", Tag = "InventoryAddProduct", IsChild = true },
        new() { Icon = "Boxes", Text = "Receive Stock", Tag = "InventoryReceiveStock", IsChild = true },
        new() { Icon = "Truck", Text = "Suppliers", Tag = "InventorySuppliers", IsChild = true },
        new() { Icon = "ArrowLeftRight", Text = "Stock Movements", Tag = "InventoryMovements", IsChild = true },
        new() { Icon = "ChartColumn", Text = "Reports", Tag = "Reports" },
    };

    public static List<PatientRecord> Patients => AddGenerated(new()
    {
        new() { PatientId = "MRN-001", LastName = "Dela Cruz", FirstName = "Juan", Age = "45", Gender = "Male", Contact = "0917-555-0101", Status = "Admitted" },
        new() { PatientId = "MRN-002", LastName = "Santos", FirstName = "Maria", Age = "32", Gender = "Female", Contact = "0918-555-0202", Status = "Outpatient" },
        new() { PatientId = "MRN-003", LastName = "Reyes", FirstName = "Pedro", Age = "67", Gender = "Male", Contact = "0919-555-0303", Status = "ER" },
        new() { PatientId = "MRN-004", LastName = "Gonzales", FirstName = "Ana", Age = "28", Gender = "Female", Contact = "0920-555-0404", Status = "Discharged" },
        new() { PatientId = "MRN-005", LastName = "Rizal", FirstName = "Jose", Age = "55", Gender = "Male", Contact = "0921-555-0505", Status = "Admitted" },
        new() { PatientId = "MRN-006", LastName = "Magsaysay", FirstName = "Cynthia", Age = "41", Gender = "Female", Contact = "0922-555-0606", Status = "Outpatient" },
        new() { PatientId = "MRN-007", LastName = "Aquino", FirstName = "Benigno", Age = "73", Gender = "Male", Contact = "0923-555-0707", Status = "Admitted" },
        new() { PatientId = "MRN-008", LastName = "Luna", FirstName = "Antonia", Age = "26", Gender = "Female", Contact = "0924-555-0808", Status = "ER" },
        new() { PatientId = "MRN-009", LastName = "Bonifacio", FirstName = "Andres", Age = "59", Gender = "Male", Contact = "0925-555-0909", Status = "Admitted" },
        new() { PatientId = "MRN-010", LastName = "Jacinto", FirstName = "Emilia", Age = "34", Gender = "Female", Contact = "0926-555-1010", Status = "Outpatient" },
        new() { PatientId = "MRN-011", LastName = "Silang", FirstName = "Gabriela", Age = "38", Gender = "Female", Contact = "0927-555-1111", Status = "Admitted" },
        new() { PatientId = "MRN-012", LastName = "Mabini", FirstName = "Apolinario", Age = "49", Gender = "Male", Contact = "0928-555-1212", Status = "Outpatient" },
        new() { PatientId = "MRN-013", LastName = "Agoncillo", FirstName = "Felipe", Age = "62", Gender = "Male", Contact = "0929-555-1313", Status = "Discharged" },
        new() { PatientId = "MRN-014", LastName = "Nakpil", FirstName = "Gregoria", Age = "30", Gender = "Female", Contact = "0930-555-1414", Status = "ER" },
        new() { PatientId = "MRN-015", LastName = "Del Pilar", FirstName = "Marcelo", Age = "57", Gender = "Male", Contact = "0931-555-1515", Status = "Admitted" },
    }, GeneratePatients());

    public static List<InvoiceRecord> Invoices => AddGenerated(new()
    {
        new() { InvoiceNo = "INV-001", Patient = "Juan Dela Cruz", Service = "ER Consultation", Amount = "Php 3,500", Insurance = "PhilHealth", Status = "Pending" },
        new() { InvoiceNo = "INV-002", Patient = "Maria Santos", Service = "Laboratory", Amount = "Php 1,200", Insurance = "Private", Status = "Paid" },
        new() { InvoiceNo = "INV-003", Patient = "Pedro Reyes", Service = "Admission", Amount = "Php 15,000", Insurance = "PhilHealth", Status = "Partial" },
        new() { InvoiceNo = "INV-004", Patient = "Ana Gonzales", Service = "Pharmacy", Amount = "Php 890", Insurance = "HMO", Status = "Paid" },
        new() { InvoiceNo = "INV-005", Patient = "Jose Rizal", Service = "X-Ray", Amount = "Php 2,500", Insurance = "PhilHealth", Status = "Pending" },
        new() { InvoiceNo = "INV-006", Patient = "Cynthia Magsaysay", Service = "Consultation", Amount = "Php 800", Insurance = "None", Status = "Paid" },
        new() { InvoiceNo = "INV-007", Patient = "Benigno Aquino", Service = "Cardiac Panel", Amount = "Php 5,200", Insurance = "PhilHealth", Status = "Pending" },
        new() { InvoiceNo = "INV-008", Patient = "Antonia Luna", Service = "Surgery", Amount = "Php 45,000", Insurance = "Private", Status = "Partial" },
        new() { InvoiceNo = "INV-009", Patient = "Andres Bonifacio", Service = "Physical Therapy", Amount = "Php 1,800", Insurance = "PhilHealth", Status = "Pending" },
        new() { InvoiceNo = "INV-010", Patient = "Emilia Jacinto", Service = "Laboratory", Amount = "Php 950", Insurance = "HMO", Status = "Paid" },
        new() { InvoiceNo = "INV-011", Patient = "Gabriela Silang", Service = "Admission", Amount = "Php 18,400", Insurance = "PhilHealth", Status = "Partial" },
        new() { InvoiceNo = "INV-012", Patient = "Apolinario Mabini", Service = "Consultation", Amount = "Php 900", Insurance = "None", Status = "Paid" },
        new() { InvoiceNo = "INV-013", Patient = "Felipe Agoncillo", Service = "CT Scan", Amount = "Php 8,500", Insurance = "Private", Status = "Pending" },
        new() { InvoiceNo = "INV-014", Patient = "Gregoria Nakpil", Service = "ER Consultation", Amount = "Php 4,200", Insurance = "HMO", Status = "Paid" },
        new() { InvoiceNo = "INV-015", Patient = "Marcelo Del Pilar", Service = "Pharmacy", Amount = "Php 1,350", Insurance = "PhilHealth", Status = "Pending" },
    }, GenerateInvoices());

    public static List<MedicineRecord> Medicines => AddGenerated(new()
    {
        new() { MedicineCode = "MED-001", MedicineName = "Paracetamol 500mg", Category = "Analgesic", Stock = "450", UnitPrice = "Php 5", ExpiryDate = "2027-06-15", Status = "In Stock" },
        new() { MedicineCode = "MED-002", MedicineName = "Amoxicillin 250mg", Category = "Antibiotic", Stock = "200", UnitPrice = "Php 12", ExpiryDate = "2026-12-20", Status = "In Stock" },
        new() { MedicineCode = "MED-003", MedicineName = "Losartan 50mg", Category = "Antihypertensive", Stock = "15", UnitPrice = "Php 8", ExpiryDate = "2026-09-30", Status = "Low Stock" },
        new() { MedicineCode = "MED-004", MedicineName = "Insulin Glargine", Category = "Antidiabetic", Stock = "0", UnitPrice = "Php 350", ExpiryDate = "2026-08-01", Status = "Out of Stock" },
        new() { MedicineCode = "MED-005", MedicineName = "Salbutamol Inhaler", Category = "Respiratory", Stock = "30", UnitPrice = "Php 250", ExpiryDate = "2027-03-10", Status = "In Stock" },
        new() { MedicineCode = "MED-006", MedicineName = "Omeprazole 20mg", Category = "Antacid", Stock = "180", UnitPrice = "Php 7", ExpiryDate = "2027-09-01", Status = "In Stock" },
        new() { MedicineCode = "MED-007", MedicineName = "Metformin 500mg", Category = "Antidiabetic", Stock = "90", UnitPrice = "Php 6", ExpiryDate = "2027-01-20", Status = "In Stock" },
        new() { MedicineCode = "MED-008", MedicineName = "Ciprofloxacin 500mg", Category = "Antibiotic", Stock = "8", UnitPrice = "Php 18", ExpiryDate = "2026-11-15", Status = "Low Stock" },
        new() { MedicineCode = "MED-009", MedicineName = "Ibuprofen 400mg", Category = "NSAID", Stock = "300", UnitPrice = "Php 4", ExpiryDate = "2027-05-30", Status = "In Stock" },
        new() { MedicineCode = "MED-010", MedicineName = "Amlodipine 5mg", Category = "Antihypertensive", Stock = "0", UnitPrice = "Php 9", ExpiryDate = "2026-10-10", Status = "Out of Stock" },
        new() { MedicineCode = "MED-011", MedicineName = "Cetirizine 10mg", Category = "Antihistamine", Stock = "140", UnitPrice = "Php 6", ExpiryDate = "2027-08-18", Status = "In Stock" },
        new() { MedicineCode = "MED-012", MedicineName = "Azithromycin 500mg", Category = "Antibiotic", Stock = "22", UnitPrice = "Php 35", ExpiryDate = "2027-02-14", Status = "Low Stock" },
        new() { MedicineCode = "MED-013", MedicineName = "Atorvastatin 20mg", Category = "Cardiovascular", Stock = "75", UnitPrice = "Php 15", ExpiryDate = "2027-07-25", Status = "In Stock" },
        new() { MedicineCode = "MED-014", MedicineName = "Prednisone 10mg", Category = "Corticosteroid", Stock = "55", UnitPrice = "Php 11", ExpiryDate = "2027-04-12", Status = "In Stock" },
        new() { MedicineCode = "MED-015", MedicineName = "Clopidogrel 75mg", Category = "Antiplatelet", Stock = "9", UnitPrice = "Php 22", ExpiryDate = "2026-12-05", Status = "Low Stock" },
    }, GenerateMedicines());

    public static List<LabOrderRecord> PendingLabOrders => AddGenerated(new()
    {
        new() { OrderNo = "LAB-001", Patient = "Juan Dela Cruz", TestType = "CBC", OrderedBy = "Dr. Santos", DateOrdered = "2026-07-13", Priority = "STAT" },
        new() { OrderNo = "LAB-002", Patient = "Maria Santos", TestType = "Urinalysis", OrderedBy = "Dr. Reyes", DateOrdered = "2026-07-13", Priority = "Routine" },
        new() { OrderNo = "LAB-003", Patient = "Pedro Reyes", TestType = "Blood Chemistry", OrderedBy = "Dr. Cruz", DateOrdered = "2026-07-12", Priority = "Routine" },
        new() { OrderNo = "LAB-006", Patient = "Benigno Aquino", TestType = "Troponin I", OrderedBy = "Dr. Santos", DateOrdered = "2026-07-13", Priority = "STAT" },
        new() { OrderNo = "LAB-007", Patient = "Antonia Luna", TestType = "Crossmatch", OrderedBy = "Dr. Reyes", DateOrdered = "2026-07-13", Priority = "STAT" },
        new() { OrderNo = "LAB-008", Patient = "Cynthia Magsaysay", TestType = "Thyroid Panel", OrderedBy = "Dr. Cruz", DateOrdered = "2026-07-12", Priority = "Routine" },
        new() { OrderNo = "LAB-012", Patient = "Gabriela Silang", TestType = "CBC", OrderedBy = "Dr. Lim", DateOrdered = "2026-07-14", Priority = "Routine" },
        new() { OrderNo = "LAB-013", Patient = "Apolinario Mabini", TestType = "Electrolytes", OrderedBy = "Dr. Santos", DateOrdered = "2026-07-14", Priority = "STAT" },
        new() { OrderNo = "LAB-014", Patient = "Felipe Agoncillo", TestType = "HbA1c", OrderedBy = "Dr. Reyes", DateOrdered = "2026-07-14", Priority = "Routine" },
        new() { OrderNo = "LAB-015", Patient = "Gregoria Nakpil", TestType = "Pregnancy Test", OrderedBy = "Dr. Cruz", DateOrdered = "2026-07-14", Priority = "STAT" },
        new() { OrderNo = "LAB-016", Patient = "Marcelo Del Pilar", TestType = "Liver Panel", OrderedBy = "Dr. Lim", DateOrdered = "2026-07-13", Priority = "Routine" },
    }, GeneratePendingLabOrders());

    public static List<LabResultRecord> CompletedLabResults => AddGenerated(new()
    {
        new() { OrderNo = "LAB-004", Patient = "Ana Gonzales", TestType = "FBS", Result = "95 mg/dL (Normal)", Completed = "2026-07-13" },
        new() { OrderNo = "LAB-005", Patient = "Jose Rizal", TestType = "Lipid Profile", Result = "See attached report", Completed = "2026-07-12" },
        new() { OrderNo = "LAB-009", Patient = "Andres Bonifacio", TestType = "PT/INR", Result = "INR 1.1 (Normal)", Completed = "2026-07-13" },
        new() { OrderNo = "LAB-010", Patient = "Emilia Jacinto", TestType = "Creatinine", Result = "0.9 mg/dL (Normal)", Completed = "2026-07-12" },
        new() { OrderNo = "LAB-011", Patient = "Juan Dela Cruz", TestType = "Blood Culture", Result = "Negative", Completed = "2026-07-11" },
        new() { OrderNo = "LAB-017", Patient = "Maria Santos", TestType = "CBC", Result = "Within normal limits", Completed = "2026-07-14" },
        new() { OrderNo = "LAB-018", Patient = "Pedro Reyes", TestType = "Electrolytes", Result = "Sodium 139 mmol/L", Completed = "2026-07-14" },
        new() { OrderNo = "LAB-019", Patient = "Ana Gonzales", TestType = "Urinalysis", Result = "No significant findings", Completed = "2026-07-14" },
        new() { OrderNo = "LAB-020", Patient = "Jose Rizal", TestType = "HbA1c", Result = "5.6%", Completed = "2026-07-13" },
        new() { OrderNo = "LAB-021", Patient = "Antonia Luna", TestType = "D-Dimer", Result = "Negative", Completed = "2026-07-13" },
        new() { OrderNo = "LAB-022", Patient = "Benigno Aquino", TestType = "Troponin I", Result = "Normal", Completed = "2026-07-13" },
    }, GenerateCompletedLabResults());

    public static List<RadiologyRecord> RadiologyOrders => AddGenerated(new()
    {
        new() { OrderNo = "XR-001", Patient = "Juan Dela Cruz", Procedure = "Chest X-Ray PA", OrderedBy = "Dr. Santos", Schedule = "2026-07-13", Status = "Pending" },
        new() { OrderNo = "XR-002", Patient = "Pedro Reyes", Procedure = "CT Scan Head", OrderedBy = "Dr. Cruz", Schedule = "2026-07-13", Status = "STAT" },
        new() { OrderNo = "XR-003", Patient = "Ana Gonzales", Procedure = "Abdominal Ultrasound", OrderedBy = "Dr. Reyes", Schedule = "2026-07-14", Status = "Scheduled" },
        new() { OrderNo = "XR-004", Patient = "Jose Rizal", Procedure = "MRI Lumbar Spine", OrderedBy = "Dr. Santos", Schedule = "2026-07-12", Status = "Completed" },
        new() { OrderNo = "XR-005", Patient = "Antonia Luna", Procedure = "X-Ray Left Tibia", OrderedBy = "Dr. Cruz", Schedule = "2026-07-13", Status = "Pending" },
        new() { OrderNo = "XR-006", Patient = "Benigno Aquino", Procedure = "2D Echo", OrderedBy = "Dr. Santos", Schedule = "2026-07-14", Status = "Scheduled" },
        new() { OrderNo = "XR-007", Patient = "Cynthia Magsaysay", Procedure = "Mammogram", OrderedBy = "Dr. Reyes", Schedule = "2026-07-15", Status = "Scheduled" },
        new() { OrderNo = "XR-008", Patient = "Gabriela Silang", Procedure = "Chest X-Ray", OrderedBy = "Dr. Lim", Schedule = "2026-07-15", Status = "Pending" },
        new() { OrderNo = "XR-009", Patient = "Apolinario Mabini", Procedure = "CT Abdomen", OrderedBy = "Dr. Santos", Schedule = "2026-07-16", Status = "Scheduled" },
        new() { OrderNo = "XR-010", Patient = "Felipe Agoncillo", Procedure = "Knee X-Ray", OrderedBy = "Dr. Cruz", Schedule = "2026-07-14", Status = "Completed" },
        new() { OrderNo = "XR-011", Patient = "Gregoria Nakpil", Procedure = "Pelvic Ultrasound", OrderedBy = "Dr. Reyes", Schedule = "2026-07-16", Status = "Scheduled" },
    }, GenerateRadiologyOrders());

    public static List<MedicalRecord> MedicalRecords => AddGenerated(new()
    {
        new() { MRN = "MRN-001", PatientName = "Dela Cruz, Juan", LastVisit = "2026-07-13", RecordStatus = "Active", ChartComplete = "Incomplete", Location = "Ward 3B" },
        new() { MRN = "MRN-002", PatientName = "Santos, Maria", LastVisit = "2026-07-12", RecordStatus = "Active", ChartComplete = "Complete", Location = "Records Room" },
        new() { MRN = "MRN-003", PatientName = "Reyes, Pedro", LastVisit = "2026-07-11", RecordStatus = "Discharged", ChartComplete = "Delinquent", Location = "Records Room" },
        new() { MRN = "MRN-005", PatientName = "Rizal, Jose", LastVisit = "2026-07-10", RecordStatus = "Active", ChartComplete = "Incomplete", Location = "Ward 2A" },
        new() { MRN = "MRN-007", PatientName = "Aquino, Benigno", LastVisit = "2026-07-13", RecordStatus = "Active", ChartComplete = "Complete", Location = "CCU" },
        new() { MRN = "MRN-009", PatientName = "Bonifacio, Andres", LastVisit = "2026-07-13", RecordStatus = "Active", ChartComplete = "Incomplete", Location = "Ward 1C" },
        new() { MRN = "MRN-010", PatientName = "Jacinto, Emilia", LastVisit = "2026-07-12", RecordStatus = "Active", ChartComplete = "Complete", Location = "Records Room" },
        new() { MRN = "MRN-011", PatientName = "Silang, Gabriela", LastVisit = "2026-07-14", RecordStatus = "Active", ChartComplete = "Incomplete", Location = "Ward 4A" },
        new() { MRN = "MRN-012", PatientName = "Mabini, Apolinario", LastVisit = "2026-07-14", RecordStatus = "Active", ChartComplete = "Complete", Location = "Ward 2B" },
        new() { MRN = "MRN-013", PatientName = "Agoncillo, Felipe", LastVisit = "2026-07-11", RecordStatus = "Discharged", ChartComplete = "Complete", Location = "Records Room" },
        new() { MRN = "MRN-014", PatientName = "Nakpil, Gregoria", LastVisit = "2026-07-14", RecordStatus = "Active", ChartComplete = "Incomplete", Location = "ER" },
    }, GenerateMedicalRecords());

    public static List<StaffRecord> Staff => AddGenerated(new()
    {
        new() { StaffId = "STF-001", Name = "Dr. Maria Santos", Department = "ER", Role = "Physician", Status = "Active" },
        new() { StaffId = "STF-002", Name = "Nurse Ana Gonzales", Department = "Ward 3B", Role = "Nurse", Status = "Active" },
        new() { StaffId = "STF-003", Name = "Pedro Reyes", Department = "Pharmacy", Role = "Pharmacist", Status = "Active" },
        new() { StaffId = "STF-004", Name = "Juan Dela Cruz", Department = "Medical Records", Role = "MR Officer", Status = "Inactive" },
        new() { StaffId = "STF-005", Name = "Dr. Jose Rizal", Department = "Radiology", Role = "Radiologist", Status = "Active" },
        new() { StaffId = "STF-006", Name = "Nurse Benigno Aquino", Department = "CCU", Role = "Head Nurse", Status = "Active" },
        new() { StaffId = "STF-007", Name = "Cynthia Magsaysay", Department = "Laboratory", Role = "Lab Technician", Status = "Active" },
        new() { StaffId = "STF-008", Name = "Emilia Jacinto", Department = "Billing", Role = "Cashier", Status = "Active" },
        new() { StaffId = "STF-009", Name = "Dr. Gabriela Silang", Department = "Pediatrics", Role = "Physician", Status = "Active" },
        new() { StaffId = "STF-010", Name = "Apolinario Mabini", Department = "Administration", Role = "Administrator", Status = "Active" },
        new() { StaffId = "STF-011", Name = "Gregoria Nakpil", Department = "Laboratory", Role = "Pathologist", Status = "Active" },
    }, GenerateStaff());

    private static List<T> AddGenerated<T>(List<T> records, IEnumerable<T> generated)
    {
        records.AddRange(generated);
        return records;
    }

    private static IEnumerable<PatientRecord> GeneratePatients() =>
        Enumerable.Range(16, 45).Select(i => new PatientRecord
        {
            PatientId = $"MRN-{i:000}", LastName = $"Patient {i}", FirstName = $"Sample {i}",
            Age = $"{18 + i % 65}", Gender = i % 2 == 0 ? "Female" : "Male",
            Contact = $"09{20 + i % 10}-555-{i:0000}",
            Status = new[] { "Admitted", "Outpatient", "ER", "Discharged" }[i % 4]
        });

    private static IEnumerable<InvoiceRecord> GenerateInvoices() =>
        Enumerable.Range(16, 45).Select(i => new InvoiceRecord
        {
            InvoiceNo = $"INV-{i:000}", Patient = $"Sample Patient {i}",
            Service = new[] { "Consultation", "Laboratory", "Pharmacy", "Radiology", "Admission" }[i % 5],
            Amount = $"Php {500 + i * 175:N0}", Insurance = new[] { "PhilHealth", "HMO", "Private", "None" }[i % 4],
            Status = new[] { "Pending", "Paid", "Partial" }[i % 3]
        });

    private static IEnumerable<MedicineRecord> GenerateMedicines() =>
        Enumerable.Range(16, 45).Select(i => new MedicineRecord
        {
            MedicineCode = $"MED-{i:000}", MedicineName = $"Sample Medicine {i}",
            Category = new[] { "Analgesic", "Antibiotic", "Cardiovascular", "Respiratory" }[i % 4],
            Stock = $"{(i % 7 == 0 ? 0 : i * 9)}", UnitPrice = $"Php {5 + i % 40}",
            ExpiryDate = $"202{7 + i % 2}-{1 + i % 12:00}-{1 + i % 28:00}",
            Status = i % 7 == 0 ? "Out of Stock" : i % 5 == 0 ? "Low Stock" : "In Stock"
        });

    private static IEnumerable<LabOrderRecord> GeneratePendingLabOrders() =>
        Enumerable.Range(12, 49).Select(i => new LabOrderRecord
        {
            OrderNo = $"LAB-P{i:000}", Patient = $"Sample Patient {i}",
            TestType = new[] { "CBC", "Urinalysis", "Blood Chemistry", "Lipid Profile" }[i % 4],
            OrderedBy = $"Dr. Sample {1 + i % 8}", DateOrdered = $"2026-07-{1 + i % 28:00}",
            Priority = i % 4 == 0 ? "STAT" : "Routine"
        });

    private static IEnumerable<LabResultRecord> GenerateCompletedLabResults() =>
        Enumerable.Range(12, 49).Select(i => new LabResultRecord
        {
            OrderNo = $"LAB-R{i:000}", Patient = $"Sample Patient {i}",
            TestType = new[] { "CBC", "FBS", "Creatinine", "Electrolytes" }[i % 4],
            Result = i % 5 == 0 ? "See attached report" : "Within normal limits",
            Completed = $"2026-07-{1 + i % 28:00}"
        });

    private static IEnumerable<RadiologyRecord> GenerateRadiologyOrders() =>
        Enumerable.Range(12, 49).Select(i => new RadiologyRecord
        {
            OrderNo = $"XR-{i:000}", Patient = $"Sample Patient {i}",
            Procedure = new[] { "Chest X-Ray", "CT Scan", "Ultrasound", "MRI" }[i % 4],
            OrderedBy = $"Dr. Sample {1 + i % 8}", Schedule = $"2026-07-{1 + i % 28:00}",
            Status = new[] { "Pending", "Scheduled", "Completed", "STAT" }[i % 4]
        });

    private static IEnumerable<MedicalRecord> GenerateMedicalRecords() =>
        Enumerable.Range(15, 49).Select((i, index) => new MedicalRecord
        {
            MRN = $"MRN-{i:000}", PatientName = $"Sample, Patient {i}", LastVisit = $"2026-07-{1 + i % 28:00}",
            RecordStatus = i % 5 == 0 ? "Discharged" : "Active",
            ChartComplete = i % 3 == 0 ? "Incomplete" : "Complete",
            Location = new[] { "Records Room", "Ward 2A", "Ward 3B", "ER" }[index % 4]
        });

    private static IEnumerable<StaffRecord> GenerateStaff() =>
        Enumerable.Range(12, 49).Select(i => new StaffRecord
        {
            StaffId = $"STF-{i:000}", Name = $"Sample Staff {i}",
            Department = new[] { "ER", "Laboratory", "Pharmacy", "Billing", "Radiology" }[i % 5],
            Role = new[] { "Physician", "Nurse", "Technician", "Officer" }[i % 4],
            Status = i % 9 == 0 ? "Inactive" : "Active"
        });
}
