using System.Text.Json;
using ClosedXML.Excel;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Exports PowerApps schema to JSON or XLSX formats.
/// </summary>
public class SchemaExporter : ISchemaExporter
{
    private readonly IFileWriter _fileWriter;

    public SchemaExporter(IFileWriter fileWriter)
    {
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public async Task ExportAsync(PowerAppsSchema schema, string outputPath, string format)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        format = format.ToLowerInvariant();

        switch (format)
        {
            case "json":
                await ExportToJsonAsync(schema, outputPath);
                break;
            case "xlsx":
                await ExportToExcelAsync(schema, outputPath);
                break;
            default:
                throw new ArgumentException($"Unsupported format: {format}", nameof(format));
        }
    }

    private async Task ExportToJsonAsync(PowerAppsSchema schema, string outputPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(schema, options);
        await _fileWriter.WriteTextAsync(outputPath, json);
    }

    private async Task ExportToExcelAsync(PowerAppsSchema schema, string outputPath)
    {
        using var workbook = new XLWorkbook();

        // Summary sheet with all entities
        var summarySheet = workbook.Worksheets.Add("Summary");
        CreateSummarySheet(summarySheet, schema);

        // Create individual entity worksheets
        if (schema.Entities != null)
        {
            foreach (var entity in schema.Entities.OrderBy(e => e.DisplayName))
            {
                var safeSheetName = GetSafeSheetName(entity.DisplayName ?? entity.LogicalName ?? "Entity");
                var entitySheet = workbook.Worksheets.Add(safeSheetName);
                CreateEntitySheet(entitySheet, entity);
            }
        }

        // Attributes sheet with all attributes from all entities
        var attributesSheet = workbook.Worksheets.Add("Attributes");
        CreateAttributesSheet(attributesSheet, schema);

        // Relationships sheet
        if (schema.Relationships?.Any() == true)
        {
            var relationshipsSheet = workbook.Worksheets.Add("Relationships");
            CreateRelationshipsSheet(relationshipsSheet, schema);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        await _fileWriter.WriteBytesAsync(outputPath, stream.ToArray());
    }

    private static void CreateSummarySheet(IXLWorksheet sheet, PowerAppsSchema schema)
    {
        int currentRow = 1;
        
        // Metadata section at top
        sheet.Cell(currentRow, 1).Value = "Environment:";
        sheet.Cell(currentRow, 1).Style.Font.Bold = true;
        sheet.Cell(currentRow, 2).Value = schema.EnvironmentUrl;
        currentRow++;
        
        sheet.Cell(currentRow, 1).Value = "Organisation:";
        sheet.Cell(currentRow, 1).Style.Font.Bold = true;
        sheet.Cell(currentRow, 2).Value = schema.OrganisationName;
        currentRow++;
        
        // Solution label - use plural if multiple solutions
        var solutionLabel = (schema.SolutionNames?.Count ?? 0) > 1 ? "Solutions:" : "Solution:";
        sheet.Cell(currentRow, 1).Value = solutionLabel;
        sheet.Cell(currentRow, 1).Style.Font.Bold = true;
        sheet.Cell(currentRow, 2).Value = schema.SolutionNames != null && schema.SolutionNames.Any()
            ? string.Join(", ", schema.SolutionNames)
            : string.Empty;
        currentRow++;
        
        sheet.Cell(currentRow, 1).Value = "Extracted:";
        sheet.Cell(currentRow, 1).Style.Font.Bold = true;
        sheet.Cell(currentRow, 2).Value = schema.ExtractedDate.ToString("yyyy-MM-dd HH:mm:ss");
        currentRow++;
        
        currentRow++; // Blank row
        
        sheet.Cell(currentRow, 1).Value = "Statistics:";
        sheet.Cell(currentRow, 1).Style.Font.Bold = true;
        currentRow++;
        
        sheet.Cell(currentRow, 1).Value = "Total Entities:";
        sheet.Cell(currentRow, 1).Style.Font.Bold = true;
        sheet.Cell(currentRow, 2).Value = schema.Entities?.Count ?? 0;
        currentRow++;
        
        var totalAttributes = schema.Entities?.Sum(e => e.Attributes?.Count ?? 0) ?? 0;
        sheet.Cell(currentRow, 1).Value = "Total Attributes:";
        sheet.Cell(currentRow, 1).Style.Font.Bold = true;
        sheet.Cell(currentRow, 2).Value = totalAttributes;
        currentRow++;
        
        sheet.Cell(currentRow, 1).Value = "Total Relationships:";
        sheet.Cell(currentRow, 1).Style.Font.Bold = true;
        sheet.Cell(currentRow, 2).Value = schema.Relationships?.Count ?? 0;
        currentRow++;
        
        currentRow += 2; // Two blank rows before entity table
        
        var headerRow = currentRow;
        
        // Headers - removed Description and Primary Name columns
        sheet.Cell(headerRow, 1).Value = "Logical Name";
        sheet.Cell(headerRow, 2).Value = "Display Name";
        sheet.Cell(headerRow, 3).Value = "Schema Name";
        sheet.Cell(headerRow, 4).Value = "Primary ID";
        sheet.Cell(headerRow, 5).Value = "Is Custom";
        sheet.Cell(headerRow, 6).Value = "Is Audit Enabled";
        sheet.Cell(headerRow, 7).Value = "Solutions";

        // Style headers
        var headerRange = sheet.Range(headerRow, 1, headerRow, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

        // Data rows
        int lastRow = headerRow;
        if (schema.Entities != null)
        {
            int row = headerRow + 1;
            foreach (var entity in schema.Entities.OrderBy(e => e.DisplayName))
            {
                var safeSheetName = GetSafeSheetName(entity.DisplayName ?? entity.LogicalName ?? "Entity");
                
                // Logical Name with hyperlink to entity sheet
                sheet.Cell(row, 1).Value = entity.LogicalName ?? string.Empty;
                sheet.Cell(row, 1).SetHyperlink(new XLHyperlink($"'{safeSheetName}'!A1"));
                sheet.Cell(row, 1).Style.Font.FontColor = XLColor.Blue;
                sheet.Cell(row, 1).Style.Font.Underline = XLFontUnderlineValues.Single;
                
                // Display Name with hyperlink to entity sheet
                sheet.Cell(row, 2).Value = entity.DisplayName ?? string.Empty;
                sheet.Cell(row, 2).SetHyperlink(new XLHyperlink($"'{safeSheetName}'!A1"));
                sheet.Cell(row, 2).Style.Font.FontColor = XLColor.Blue;
                sheet.Cell(row, 2).Style.Font.Underline = XLFontUnderlineValues.Single;
                
                sheet.Cell(row, 3).Value = entity.SchemaName ?? string.Empty;
                sheet.Cell(row, 4).Value = entity.PrimaryIdAttribute ?? string.Empty;
                sheet.Cell(row, 5).Value = entity.IsCustomEntity.ToString();
                sheet.Cell(row, 6).Value = entity.IsAuditEnabled.ToString();
                sheet.Cell(row, 7).Value = entity.FoundInSolutions != null && entity.FoundInSolutions.Any()
                    ? string.Join(", ", entity.FoundInSolutions)
                    : string.Empty;
                row++;
            }
            lastRow = row - 1;
        }

        // Convert to Excel table
        if (schema.Entities != null && schema.Entities.Any())
        {
            var tableRange = sheet.Range(headerRow, 1, lastRow, 7);
            var table = tableRange.CreateTable("EntitiesTable");
            table.Theme = XLTableTheme.TableStyleMedium2;
        }

        // Auto-fit columns
        sheet.Columns().AdjustToContents();
    }

    private static void CreateAttributesSheet(IXLWorksheet sheet, PowerAppsSchema schema)
    {
        // Headers
        sheet.Cell(1, 1).Value = "Entity";
        sheet.Cell(1, 2).Value = "Logical Name";
        sheet.Cell(1, 3).Value = "Display Name";
        sheet.Cell(1, 4).Value = "Schema Name";
        sheet.Cell(1, 5).Value = "Type";
        sheet.Cell(1, 6).Value = "Description";
        sheet.Cell(1, 7).Value = "Is Primary ID";
        sheet.Cell(1, 8).Value = "Is Primary Name";
        sheet.Cell(1, 9).Value = "Is Custom";
        sheet.Cell(1, 10).Value = "Is Audit Enabled";
        sheet.Cell(1, 11).Value = "Required";
        sheet.Cell(1, 12).Value = "Max Length";
        sheet.Cell(1, 13).Value = "Min Value";
        sheet.Cell(1, 14).Value = "Max Value";
        sheet.Cell(1, 15).Value = "Precision";
        sheet.Cell(1, 16).Value = "Targets";

        // Style headers
        var headerRange = sheet.Range(1, 1, 1, 16);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGreen;

        // Data rows
        if (schema.Entities != null)
        {
            int row = 2;
            foreach (var entity in schema.Entities.OrderBy(e => e.DisplayName))
            {
                if (entity.Attributes != null)
                {
                    foreach (var attr in entity.Attributes.OrderBy(a => a.DisplayName))
                    {
                        sheet.Cell(row, 1).Value = entity.LogicalName ?? string.Empty;
                        sheet.Cell(row, 2).Value = attr.LogicalName ?? string.Empty;
                        sheet.Cell(row, 3).Value = attr.DisplayName ?? string.Empty;
                        sheet.Cell(row, 4).Value = attr.SchemaName ?? string.Empty;
                        sheet.Cell(row, 5).Value = attr.AttributeType ?? string.Empty;
                        sheet.Cell(row, 6).Value = attr.Description ?? string.Empty;
                        sheet.Cell(row, 7).Value = attr.IsPrimaryId.ToString();
                        sheet.Cell(row, 8).Value = attr.IsPrimaryName.ToString();
                        sheet.Cell(row, 9).Value = attr.IsCustomAttribute.ToString();
                        sheet.Cell(row, 10).Value = attr.IsAuditEnabled.ToString();
                        sheet.Cell(row, 11).Value = attr.RequiredLevel ?? string.Empty;
                        sheet.Cell(row, 12).Value = attr.MaxLength?.ToString() ?? string.Empty;
                        sheet.Cell(row, 13).Value = attr.MinValue?.ToString() ?? string.Empty;
                        sheet.Cell(row, 14).Value = attr.MaxValue?.ToString() ?? string.Empty;
                        sheet.Cell(row, 15).Value = attr.Precision?.ToString() ?? string.Empty;
                        sheet.Cell(row, 16).Value = attr.Targets != null && attr.Targets.Any()
                            ? string.Join(", ", attr.Targets)
                            : string.Empty;
                        row++;
                    }
                }
            }
        }

        // Auto-fit columns
        sheet.Columns().AdjustToContents();
    }

    private static void CreateRelationshipsSheet(IXLWorksheet sheet, PowerAppsSchema schema)
    {
        // Headers
        sheet.Cell(1, 1).Value = "Schema Name";
        sheet.Cell(1, 2).Value = "Type";
        sheet.Cell(1, 3).Value = "Referenced Entity";
        sheet.Cell(1, 4).Value = "Referenced Attribute";
        sheet.Cell(1, 5).Value = "Referencing Entity";
        sheet.Cell(1, 6).Value = "Referencing Attribute";
        sheet.Cell(1, 7).Value = "Relationship Type";
        sheet.Cell(1, 8).Value = "Is Custom";

        // Style headers
        var headerRange = sheet.Range(1, 1, 1, 8);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightYellow;

        int row = 2;

        // All relationships
        if (schema.Relationships != null)
        {
            foreach (var rel in schema.Relationships.OrderBy(r => r.SchemaName))
            {
                sheet.Cell(row, 1).Value = rel.SchemaName ?? string.Empty;
                sheet.Cell(row, 2).Value = rel.RelationshipType ?? "Unknown";
                
                // For OneToMany relationships
                if (rel.RelationshipType == "OneToManyRelationship")
                {
                    sheet.Cell(row, 3).Value = rel.ReferencedEntity ?? string.Empty;
                    sheet.Cell(row, 4).Value = rel.ReferencedAttribute ?? string.Empty;
                    sheet.Cell(row, 5).Value = rel.ReferencingEntity ?? string.Empty;
                    sheet.Cell(row, 6).Value = rel.ReferencingAttribute ?? string.Empty;
                    sheet.Cell(row, 7).Value = string.Empty;
                }
                // For ManyToMany relationships
                else if (rel.RelationshipType == "ManyToManyRelationship")
                {
                    sheet.Cell(row, 3).Value = rel.Entity1LogicalName ?? string.Empty;
                    sheet.Cell(row, 4).Value = string.Empty; // ManyToMany doesn't have specific intersection attributes in model
                    sheet.Cell(row, 5).Value = rel.Entity2LogicalName ?? string.Empty;
                    sheet.Cell(row, 6).Value = string.Empty;
                    sheet.Cell(row, 7).Value = rel.IntersectEntityName ?? string.Empty;
                }
                
                sheet.Cell(row, 8).Value = rel.IsCustomRelationship.ToString();
                row++;
            }
        }

        // Auto-fit columns
        sheet.Columns().AdjustToContents();
    }

    private static string GetSafeSheetName(string name)
    {
        // Excel sheet names must be <= 31 characters and cannot contain: \ / ? * [ ]
        var safeName = name;
        foreach (var c in new[] { '\\', '/', '?', '*', '[', ']' })
        {
            safeName = safeName.Replace(c, '_');
        }
        
        if (safeName.Length > 31)
        {
            safeName = safeName.Substring(0, 31);
        }
        
        return safeName;
    }

    private static string GetSafeTableName(string name)
    {
        // Excel table names must start with a letter or underscore, 
        // can only contain letters, numbers, and underscores, and be <= 255 characters
        var safeName = name;
        
        // Replace invalid characters with underscore
        safeName = new string(safeName.Select(c => 
            char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        
        // Ensure it starts with a letter or underscore
        if (!char.IsLetter(safeName[0]) && safeName[0] != '_')
        {
            safeName = "_" + safeName;
        }
        
        // Limit length
        if (safeName.Length > 250) // Leave room for "_Attributes" suffix
        {
            safeName = safeName.Substring(0, 250);
        }
        
        return safeName;
    }

    private static void CreateEntitySheet(IXLWorksheet sheet, EntitySchema entity)
    {
        // Back to Summary link
        sheet.Cell(1, 1).Value = "« Back to Summary";
        sheet.Cell(1, 1).SetHyperlink(new XLHyperlink("'Summary'!A1"));
        sheet.Cell(1, 1).Style.Font.FontColor = XLColor.Blue;
        sheet.Cell(1, 1).Style.Font.Underline = XLFontUnderlineValues.Single;
        sheet.Cell(1, 1).Style.Font.Bold = true;

        // Entity details header
        sheet.Cell(3, 1).Value = "Entity Details";
        sheet.Cell(3, 1).Style.Font.Bold = true;
        sheet.Cell(3, 1).Style.Font.FontSize = 14;

        // Entity properties
        var row = 4;
        AddPropertyRow(sheet, ref row, "Logical Name", entity.LogicalName);
        AddPropertyRow(sheet, ref row, "Display Name", entity.DisplayName);
        AddPropertyRow(sheet, ref row, "Schema Name", entity.SchemaName);
        AddPropertyRow(sheet, ref row, "Description", entity.Description);
        AddPropertyRow(sheet, ref row, "Primary ID", entity.PrimaryIdAttribute);
        AddPropertyRow(sheet, ref row, "Primary Name", entity.PrimaryNameAttribute);
        AddPropertyRow(sheet, ref row, "Is Custom", entity.IsCustomEntity.ToString());
        AddPropertyRow(sheet, ref row, "Is Audit Enabled", entity.IsAuditEnabled.ToString());
        
        if (entity.FoundInSolutions != null && entity.FoundInSolutions.Any())
        {
            AddPropertyRow(sheet, ref row, "Found In Solutions", string.Join(", ", entity.FoundInSolutions));
        }

        // Attributes section
        if (entity.Attributes != null && entity.Attributes.Any())
        {
            row += 2;
            sheet.Cell(row, 1).Value = "Attributes";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Font.FontSize = 12;
            row++;

            // Headers
            sheet.Cell(row, 1).Value = "Logical Name";
            sheet.Cell(row, 2).Value = "Display Name";
            sheet.Cell(row, 3).Value = "Type";
            sheet.Cell(row, 4).Value = "Description";
            sheet.Cell(row, 5).Value = "Required";
            sheet.Cell(row, 6).Value = "Is Audit Enabled";
            sheet.Cell(row, 7).Value = "Max Length";
            
            var headerRange = sheet.Range(row, 1, row, 7);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            var attributeHeaderRow = row;
            row++;

            // Attribute data
            var attributeStartRow = row;
            foreach (var attr in entity.Attributes.OrderBy(a => a.DisplayName))
            {
                sheet.Cell(row, 1).Value = attr.LogicalName ?? string.Empty;
                sheet.Cell(row, 2).Value = attr.DisplayName ?? string.Empty;
                sheet.Cell(row, 3).Value = attr.AttributeType ?? string.Empty;
                sheet.Cell(row, 4).Value = attr.Description ?? string.Empty;
                sheet.Cell(row, 5).Value = attr.RequiredLevel ?? string.Empty;
                sheet.Cell(row, 6).Value = attr.IsAuditEnabled.ToString();
                sheet.Cell(row, 7).Value = attr.MaxLength?.ToString() ?? string.Empty;
                row++;
            }
            
            // Convert to Excel table
            if (entity.Attributes.Count > 0)
            {
                var safeTableName = GetSafeTableName(entity.LogicalName ?? "Entity");
                var tableRange = sheet.Range(attributeHeaderRow, 1, row - 1, 7);
                var table = tableRange.CreateTable($"{safeTableName}_Attributes");
                table.Theme = XLTableTheme.TableStyleLight9;
            }
        }

        // Auto-fit columns
        sheet.Columns().AdjustToContents();
    }

    private static void AddPropertyRow(IXLWorksheet sheet, ref int row, string label, string? value)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 2).Value = value ?? string.Empty;
        row++;
    }
}
