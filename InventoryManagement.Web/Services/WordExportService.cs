using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace InventoryManagement.Web.Services
{
    public class WordExportService : IWordExportService
    {
        private readonly IWebHostEnvironment _environment;

        // Brand yellow, used for the title text and the table header.
        private const string BRAND_COLOR = "FFC000";

        public WordExportService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public byte[] GenerateDepartmentInventoryDocument(
            DepartmentViewModel department,
            List<ProductViewModel> products)
        {
            using var memoryStream = new MemoryStream();

            using (var wordDocument = WordprocessingDocument.Create(
                memoryStream, WordprocessingDocumentType.Document, true))
            {
                var mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                SetPageMargins(mainPart);

                AddHeaderWithLogoAndTitle(body, mainPart);

                body.AppendChild(CreateSmallSpacingParagraph());

                AddCommitteeSection(body);

                AddDateSection(body);

                body.AppendChild(CreateSmallSpacingParagraph());

                AddInventoryTable(body, products);

                body.AppendChild(CreateSmallSpacingParagraph());

                AddDepartmentName(body, department);

                body.AppendChild(CreateSmallSpacingParagraph());

                AddSignatureSection(body, department);
            }

            return memoryStream.ToArray();
        }



        /// <summary>Half-inch top and bottom margins so more rows fit on a page.</summary>
        private void SetPageMargins(MainDocumentPart mainPart)
        {
            var sectionProperties = new SectionProperties();
            var pageMargin = new PageMargin()
            {
                Top = 720,      // 0.5 inch
                Right = 1440U,  // 1 inch
                Bottom = 720,   // 0.5 inch
                Left = 1440U,   // 1 inch
                Header = 720U,
                Footer = 720U,
                Gutter = 0U
            };
            sectionProperties.Append(pageMargin);
            mainPart.Document.Body!.Append(sectionProperties);
        }



        /// <summary>Logo on the left and the two-line title on the right, in a borderless table.</summary>
        private void AddHeaderWithLogoAndTitle(Body body, MainDocumentPart mainPart)
        {
            var headerTable = new Table();

            var tblProp = new TableProperties();
            tblProp.Append(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });

            var tblBorders = new TableBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.None },
                new InsideVerticalBorder { Val = BorderValues.None }
            );
            tblProp.Append(tblBorders);
            tblProp.Append(new TableCellSpacing { Width = "0", Type = TableWidthUnitValues.Dxa });

            headerTable.Append(tblProp);

            var headerRow = new TableRow();

            var logoCell = new TableCell();
            var logoCellProp = new TableCellProperties();
            logoCellProp.Append(new TableCellWidth { Width = "2500", Type = TableWidthUnitValues.Dxa });
            logoCellProp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });

            var cellMargin = new TableCellMargin();
            cellMargin.Append(new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa });
            cellMargin.Append(new BottomMargin { Width = "0", Type = TableWidthUnitValues.Dxa });
            logoCellProp.Append(cellMargin);

            logoCell.Append(logoCellProp);

            var logoPara = new Paragraph();
            var logoParaProp = new ParagraphProperties();
            logoParaProp.Append(new SpacingBetweenLines { Before = "0", After = "0" });
            logoPara.Append(logoParaProp);

            try
            {
                var logoPath = Path.Combine(_environment.WebRootPath, "logo.jpg");

                if (File.Exists(logoPath))
                {
                    var logoRun = CreateImageRun(mainPart, logoPath, "Logo", 200, 200);
                    logoPara.Append(logoRun);
                }
                else
                {
                    var logoRun = CreateTextRun("[LOGO]", 24, true, true);
                    logoPara.Append(logoRun);
                }
            }
            catch
            {
                var logoRun = CreateTextRun("[LOGO]", 24, true, true);
                logoPara.Append(logoRun);
            }

            logoCell.Append(logoPara);
            headerRow.Append(logoCell);

            var titleCell = new TableCell();
            var titleCellProp = new TableCellProperties();
            titleCellProp.Append(new TableCellWidth { Width = "3000", Type = TableWidthUnitValues.Dxa });
            titleCellProp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });

            var titleCellMargin = new TableCellMargin();
            titleCellMargin.Append(new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa });
            titleCellMargin.Append(new BottomMargin { Width = "0", Type = TableWidthUnitValues.Dxa });
            titleCellProp.Append(titleCellMargin);

            titleCell.Append(titleCellProp);

            var titlePara1 = new Paragraph();
            var titleParaProp1 = new ParagraphProperties();
            titleParaProp1.Append(new Justification { Val = JustificationValues.Right });
            titleParaProp1.Append(new SpacingBetweenLines { Before = "0", After = "0", Line = "240" });
            titlePara1.Append(titleParaProp1);

            var titleRun1 = CreateColoredTextRun("IT AVADANLIQLARININ", 36, true, true, BRAND_COLOR);
            titlePara1.Append(titleRun1);
            titleCell.Append(titlePara1);

            var titlePara2 = new Paragraph();
            var titleParaProp2 = new ParagraphProperties();
            titleParaProp2.Append(new Justification { Val = JustificationValues.Right });
            titleParaProp2.Append(new SpacingBetweenLines { Before = "0", After = "0", Line = "240" });
            titlePara2.Append(titleParaProp2);

            var titleRun2 = CreateColoredTextRun("İNVENTARİZASİYASI", 36, true, true, BRAND_COLOR);
            titlePara2.Append(titleRun2);
            titleCell.Append(titlePara2);

            headerRow.Append(titleCell);
            headerTable.Append(headerRow);
            body.Append(headerTable);
        }



        /// <summary>Embeds an image file as an inline picture.</summary>
        private Run CreateImageRun(MainDocumentPart mainPart, string imagePath, string imageName, int widthInPoints, int heightInPoints)
        {
            ImagePart imagePart = mainPart.AddImagePart(ImagePartType.Png);

            using (FileStream stream = new FileStream(imagePath, FileMode.Open))
            {
                imagePart.FeedData(stream);
            }

            string relationshipId = mainPart.GetIdOfPart(imagePart);

            // 9525 EMU is one pixel at 96 DPI, so the sizes are really pixels, not points.
            long widthInEmus = widthInPoints * 9525;
            long heightInEmus = heightInPoints * 9525;

            var element = new Drawing(
                new DW.Inline(
                    new DW.Extent() { Cx = widthInEmus, Cy = heightInEmus },
                    new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties() { Id = 1U, Name = imageName },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks() { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties() { Id = 0U, Name = imageName },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip() { Embed = relationshipId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset() { X = 0L, Y = 0L },
                                        new A.Extents() { Cx = widthInEmus, Cy = heightInEmus }),
                                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                        )
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
                )
                {
                    DistanceFromTop = 0U,
                    DistanceFromBottom = 0U,
                    DistanceFromLeft = 0U,
                    DistanceFromRight = 0U
                });

            return new Run(element);
        }



        /// <summary>The handover committee heading and its member, centered.</summary>
        private void AddCommitteeSection(Body body)
        {
            var headerPara = new Paragraph();
            var headerParaProp = new ParagraphProperties();
            headerParaProp.Append(new Justification { Val = JustificationValues.Center });
            headerParaProp.Append(new SpacingBetweenLines { Before = "120", After = "60" });
            headerPara.Append(headerParaProp);

            var headerRun = CreateTextRun("Təhvil-təslim Heyəti:", 32, true, true);
            headerPara.Append(headerRun);
            body.Append(headerPara);

            var namePara = new Paragraph();
            var nameParaProp = new ParagraphProperties();
            nameParaProp.Append(new Justification { Val = JustificationValues.Center });
            nameParaProp.Append(new SpacingBetweenLines { Before = "60", After = "120" });
            namePara.Append(nameParaProp);

            var nameRun = CreateTextRun("Kənan Əhədzadə", 28, false, true);
            namePara.Append(nameRun);
            body.Append(namePara);
        }



        private void AddDateSection(Body body)
        {
            var datePara = new Paragraph();
            var dateParaProp = new ParagraphProperties();
            dateParaProp.Append(new SpacingBetweenLines { Before = "120", After = "120" });
            datePara.Append(dateParaProp);

            var dateRun = CreateTextRun($"Tarix: {DateTime.Now:dd.MM.yyyy}", 32, true, true);
            datePara.Append(dateRun);
            body.Append(datePara);
        }



        /// <summary>Product table with brand-yellow headers, black borders and a total row.</summary>
        private void AddInventoryTable(Body body, List<ProductViewModel> products)
        {
            var table = new Table();

            var tblProp = new TableProperties();
            tblProp.Append(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });

            var tblBorders = new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new BottomBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new LeftBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new RightBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 6, Color = "000000" }
            );
            tblProp.Append(tblBorders);
            tblProp.Append(new TableLayout { Type = TableLayoutValues.Fixed });

            table.Append(tblProp);

            var headerRow = new TableRow();
            headerRow.Append(CreateHeaderCell("Avadanlıq", 2000));
            headerRow.Append(CreateHeaderCell("Vendor", 2000));
            headerRow.Append(CreateHeaderCell("Model", 2000));
            headerRow.Append(CreateHeaderCell("İnventar kodu", 1500));
            table.Append(headerRow);

            var sortedProducts = products
                .OrderBy(p => p.CategoryName)
                .ThenBy(p => p.InventoryCode)
                .ToList();

            foreach (var product in sortedProducts)
            {
                var dataRow = new TableRow();

                dataRow.Append(CreateCenteredDataCell(product.CategoryName ?? "N/A"));
                dataRow.Append(CreateCenteredDataCell(product.Vendor ?? "N/A"));
                dataRow.Append(CreateCenteredDataCell(product.Model ?? "N/A"));
                dataRow.Append(CreateCenteredDataCell(product.InventoryCode.ToString()));

                table.Append(dataRow);
            }

            // The total label spans the first three columns.
            var totalRow = new TableRow();

            var totalLabelCell = new TableCell();
            var totalLabelCellProp = new TableCellProperties();
            totalLabelCellProp.Append(new GridSpan { Val = 3 });
            totalLabelCellProp.Append(new TableCellWidth { Width = "6000", Type = TableWidthUnitValues.Dxa });
            totalLabelCell.Append(totalLabelCellProp);

            var totalLabelPara = new Paragraph();
            var totalLabelParaProp = new ParagraphProperties();
            totalLabelParaProp.Append(new Justification { Val = JustificationValues.Left });
            totalLabelPara.Append(totalLabelParaProp);

            var totalLabelRun = CreateTextRun("Cəmi:", 22, true, true);
            totalLabelPara.Append(totalLabelRun);
            totalLabelCell.Append(totalLabelPara);
            totalRow.Append(totalLabelCell);

            var countCell = new TableCell();
            var countCellProp = new TableCellProperties();
            countCellProp.Append(new TableCellWidth { Width = "1500", Type = TableWidthUnitValues.Dxa });
            countCellProp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            countCell.Append(countCellProp);

            var countPara = new Paragraph();
            var countParaProp = new ParagraphProperties();
            countParaProp.Append(new Justification { Val = JustificationValues.Center });
            countPara.Append(countParaProp);

            var countRun = CreateTextRun($"{products.Count}", 22, true, true);
            countPara.Append(countRun);
            countCell.Append(countPara);
            totalRow.Append(countCell);

            table.Append(totalRow);
            body.Append(table);
        }



        private void AddDepartmentName(Body body, DepartmentViewModel department)
        {
            var deptPara = new Paragraph();
            var deptParaProp = new ParagraphProperties();
            deptParaProp.Append(new Justification { Val = JustificationValues.Left });
            deptParaProp.Append(new SpacingBetweenLines { Before = "240", After = "240" });
            deptPara.Append(deptParaProp);

            var deptRun = CreateTextRun(department.Name, 24, true, true);

            var runProps = deptRun.RunProperties;
            if (runProps != null)
            {
                runProps.Append(new Underline { Val = UnderlineValues.Single });
            }

            deptPara.Append(deptRun);
            body.Append(deptPara);
        }



        /// <summary>The handed-over and received signature lines, kept on the same page.</summary>
        private void AddSignatureSection(Body body, DepartmentViewModel department)
        {
            var signatureTable = new Table();

            var tblProp = new TableProperties();
            tblProp.Append(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });

            var tblBorders = new TableBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.None }
            );
            tblProp.Append(tblBorders);

            tblProp.Append(new TableStyle { Val = "TableGrid" });

            signatureTable.Append(tblProp);


            var transferredPara = new Paragraph();
            var transferredParaProp = new ParagraphProperties();
            transferredParaProp.Append(new SpacingBetweenLines { Before = "120", After = "120" });
            // KeepNext stops a page break from separating the two signature lines.
            transferredParaProp.Append(new KeepNext());
            transferredPara.Append(transferredParaProp);

            var transferredRun = CreateTextRun(
                "Təhvil verdi: Yusif Bağıyev ____________________",
                22,
                true,
                true);
            transferredPara.Append(transferredRun);
            body.Append(transferredPara);

            var receivedPara = new Paragraph();
            var receivedParaProp = new ParagraphProperties();
            receivedParaProp.Append(new SpacingBetweenLines { Before = "120", After = "120" });
            receivedParaProp.Append(new KeepNext());
            receivedParaProp.Append(new KeepLines());
            receivedPara.Append(receivedParaProp);

            var departmentHeadName = !string.IsNullOrEmpty(department.DepartmentHead)
                ? department.DepartmentHead
                : "_______________";

            var receivedRun = CreateTextRun(
                $"Təhvil aldı: {departmentHeadName} ____________________",
                22,
                true,
                true);
            receivedPara.Append(receivedRun);
            body.Append(receivedPara);

        }



        private Run CreateTextRun(string text, int fontSize, bool bold, bool timesNewRoman = true)
        {
            var run = new Run();
            var runProp = new RunProperties();

            if (timesNewRoman)
            {
                runProp.Append(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" });
            }

            runProp.Append(new FontSize { Val = fontSize.ToString() });

            if (bold)
            {
                runProp.Append(new Bold());
            }

            run.Append(runProp);
            run.Append(new Text(text));

            return run;
        }



        private Run CreateColoredTextRun(string text, int fontSize, bool bold, bool timesNewRoman, string color)
        {
            var run = new Run();
            var runProp = new RunProperties();

            if (timesNewRoman)
            {
                runProp.Append(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" });
            }

            runProp.Append(new FontSize { Val = fontSize.ToString() });

            if (bold)
            {
                runProp.Append(new Bold());
            }

            runProp.Append(new Color { Val = color });

            run.Append(runProp);
            run.Append(new Text(text));

            return run;
        }



        /// <summary>Text run with a highlighter-style background behind the text only.</summary>
        private Run CreateHighlightedTextRun(string text, int fontSize, bool bold, bool timesNewRoman, string highlightColor)
        {
            var run = new Run();
            var runProp = new RunProperties();

            if (timesNewRoman)
            {
                runProp.Append(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" });
            }

            runProp.Append(new FontSize { Val = fontSize.ToString() });

            if (bold)
            {
                runProp.Append(new Bold());
            }

            // Highlight takes only named colours, so highlightColor is ignored and yellow is used.
            runProp.Append(new Highlight { Val = HighlightColorValues.Yellow });

            run.Append(runProp);
            run.Append(new Text(text));

            return run;
        }



        private TableCell CreateHeaderCell(string text, int width)
        {
            var cell = new TableCell();

            var cellProp = new TableCellProperties();
            cellProp.Append(new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa });
            cellProp.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = BRAND_COLOR });
            cellProp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            cell.Append(cellProp);

            var para = new Paragraph();
            var paraProp = new ParagraphProperties();
            paraProp.Append(new Justification { Val = JustificationValues.Center });
            para.Append(paraProp);

            var run = CreateTextRun(text, 20, true, true);
            para.Append(run);
            cell.Append(para);

            return cell;
        }



        private TableCell CreateCenteredDataCell(string text)
        {
            var cell = new TableCell();

            var cellProp = new TableCellProperties();
            cellProp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            cell.Append(cellProp);

            var para = new Paragraph();
            var paraProp = new ParagraphProperties();
            paraProp.Append(new Justification { Val = JustificationValues.Center });
            para.Append(paraProp);

            var run = CreateTextRun(text, 20, false, true);
            para.Append(run);
            cell.Append(para);

            return cell;
        }



        private Paragraph CreateSmallSpacingParagraph()
        {
            var para = new Paragraph();
            var paraProp = new ParagraphProperties();
            paraProp.Append(new SpacingBetweenLines { Before = "120", After = "120" });
            para.Append(paraProp);
            para.Append(new Run(new Text("")));
            return para;
        }
    }
}