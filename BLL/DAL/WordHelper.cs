/************************不需要基于Office软件，对Word文档进行处理***************************************
 * 1、此类基于Aspose.Words类库实现；
 * 2、基于word模板的文档生成；
 * 3、根据已有书签，实现书签位置的内容替换；
 * 4、基于已有表格，增加表格的行数；
 * 5、创建新的表格；
 * 6、在文档中插入图片。
 * 作者：张伟锋       日期：2013年12月31日     版权所有：上海地听信息科技有限公司 Copyright(2013-2015)
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using Aspose.Words;
using Aspose.Words.Tables;
using Aspose.Words.Saving;
using Aspose.Words.Drawing;
using System.Configuration;
namespace MMShareBLL.DAL
{
    public class WordHelper
    {
        private Document m_Doc = null;
        private DocumentBuilder m_Builder = null;

        #region Property

        public Document Document
        {
            get { return m_Doc; }
            set { m_Doc = value; }
        }

        public DocumentBuilder Builder
        {
            get { return m_Builder; }
            set { m_Builder = value; }
        }
        public WordHelper() { }


        public WordHelper(string templateName)
        {
            CreateNewWordDocument(templateName);
        }
        #endregion


        #region 从模板创建新的Word文档
        /// <summary>  
        /// 从模板创建新的Word文档  
        /// </summary>  
        /// <param name="templateName">模板文件名</param>  
        /// <returns></returns>  
        public bool CreateNewWordDocument(string templateName)
        {
            try
            {
                m_Doc = new Document(templateName);

                m_Builder = new DocumentBuilder(m_Doc);
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 创建一个新的word文档
        /// </summary>
        public void Open()
        {
            m_Doc = new Aspose.Words.Document();
            m_Builder = new DocumentBuilder(m_Doc);
        }
        #endregion

        #region 文档另存为其他文件名
        /// <summary>  
        /// 文档另存为其他文件名，缺省情况下为2003版本  
        /// </summary>  
        /// <param name="fileName">文件名</param>  
        /// <param name="wDoc">Document对象</param>  
        public bool  SaveAs(string fileName,string pendix)
        {
            //string strFileName = fileName.Substring(0, fileName.LastIndexOf("."));
            if (pendix.Equals("Doc"))
            {
                return SaveAs(fileName + ".doc", SaveFormat.Doc);
            }
            else if (pendix.Equals("Pdf"))
            {
                return SaveAs(fileName, SaveFormat.Pdf);
            }
            else
            {
                return SaveAs(fileName + ".doc", SaveFormat.Doc);
            }
           
        }

        public void SaveToHTML(string fullOldFileName, string fullNewFileName)
        {

            Document doc = new Document(fullOldFileName);
            doc.Save(fullNewFileName, SaveFormat.Html);

        }

        /// <summary>
        /// Word转一张或多张图片
        /// </summary>
        /// <param name="fullOldFileName">原始WORD文档全路径</param>
        /// <param name="fullNewFileName">保存JPEG图片文件路径</param>
        /// <returns></returns>
        public bool SaveToImage(string fullOldFileName, string fullNewFileName)
        {
            try
            {

                string fileName = fullOldFileName.Substring(fullOldFileName.LastIndexOf("\\") + 1);
                string name = fileName.Split('.')[0];
                m_Doc = new Document(fullOldFileName);
                ImageSaveOptions iso = new ImageSaveOptions(SaveFormat.Png);
                iso.Resolution = 128;
                iso.PrettyFormat = true;
                iso.UseAntiAliasing = true;
                if (m_Doc.PageCount.Equals(1))
                    m_Doc.Save(fullNewFileName + "\\" + name + ".png", iso);
                else
                {
                    for (int i = 0; i < m_Doc.PageCount; i++)
                    {
                        iso.PageIndex = i;
                        m_Doc.Save(fullNewFileName + "\\" + name + i + ".png", iso);
                    }
                }

                return true;
            }
            catch (Exception e)
            {

                throw e;
            }

        }


        /// <summary>
        /// Word多页转一张图片
        /// </summary>
        /// <param name="fullOldFileName">原始WORD文档全路径</param>
        /// <param name="fullNewFileName">保存JPEG图片文件路径</param>
        /// <returns></returns>
        //public bool SaveToOneImage(string fullOldFileName, string imgFileName)
        //{
        //    try
        //    {
        //        List<Image> images = new List<Image>();
        //        m_Doc = new Document(fullOldFileName);
        //        ImageSaveOptions iso = new ImageSaveOptions(SaveFormat.Png);
        //        iso.Resolution = 96;
        //        iso.PrettyFormat = true;
        //        iso.UseAntiAliasing = true;
        //        iso.UseHighQualityRendering = true;
        //        if (m_Doc.PageCount.Equals(1))
        //        {
        //            m_Doc.Save(imgFileName + ".png", iso);

        //        }
        //        else
        //        {
        //            int width = 0;
        //            int height = 0;
        //            for (int i = 0; i < m_Doc.PageCount; i++)
        //            {
        //                iso.PageIndex = i;
        //                string path = imgFileName + i + ".png";
        //                m_Doc.Save(path, iso);
        //                Image image = Image.FromFile(path);
        //                height += image.Height;
        //                width = image.Width;
        //                images.Add(image);
        //            }
        //            Bitmap bigMap = new Bitmap(width, height);
        //            Graphics gp = Graphics.FromImage(bigMap);
        //            for (int i = 0; i < images.Count; i++)
        //            {

        //                gp.DrawImage(images[i], 0, i * (images[i].Height), images[i].Width, images[i].Height);
        //                images[i].Dispose();
        //            }

        //            bigMap.Save(imgFileName + ".png");
        //            bigMap.Dispose();
        //            for (int i = 0; i < m_Doc.PageCount; i++)
        //            {

        //                File.Delete(imgFileName + i + ".png");
        //            }

        //        }
        //        return true;
        //    }
        //    catch (Exception e)
        //    {

        //        throw e;
        //    }

        //}

        public bool SaveAsImageWithLine(string docPath, string imageFilePath,bool isHD)
        {
            try
            {
                List<Image> images = new List<Image>();
                var doc = new Document(docPath);
                ImageSaveOptions iso = new ImageSaveOptions(SaveFormat.Png);
                if (isHD)
                {
                    iso.Resolution = 300;
                }
                else
                {
                    iso.Resolution = 96;
                }
                iso.JpegQuality = 100;
               
                iso.PrettyFormat = true;
                iso.UseAntiAliasing = true;
                iso.UseHighQualityRendering = true;
                if (doc.PageCount.Equals(1))
                {
                    if (isHD)
                    {
                        doc.Save(imageFilePath + "_HD.png", iso);
                    }
                    else
                    {
                        doc.Save(imageFilePath + "_view.png", iso);
                    }
                }
                else
                {
                    int width = 0;
                    int height = 0;
                    for (int i = 0; i < doc.PageCount; i++)
                    {
                        iso.PageIndex = i;
                        string path = imageFilePath + i + ".png";
                        doc.Save(path, iso);
                        Image image = Image.FromFile(path);
                        height += image.Height;
                        width = image.Width;
                        images.Add(image);
                    }
                    Bitmap bigMap = new Bitmap(width, height + doc.PageCount - 1);
                    Graphics gp = Graphics.FromImage(bigMap);
                    var pen = new Pen(Color.Gray);
                    for (int i = 0; i < images.Count; i++)
                    {
                        if (i > 0)
                        {
                            gp.DrawLine(pen, 0, i * (images[i].Height) + i - 1, images[i].Width, i * (images[i].Height));
                            gp.DrawImage(images[i], 0, i * (images[i].Height) + i, images[i].Width, images[i].Height);

                        }
                        else
                        {
                            gp.DrawImage(images[i], 0, i * (images[i].Height) + i, images[i].Width, images[i].Height);

                        }
                        images[i].Dispose();
                    }
                    if (isHD)
                    {
                        bigMap.Save(imageFilePath + "_HD.png");
                    }
                    else
                    {
                        bigMap.Save(imageFilePath + "_view.png");
                    }
                    bigMap.Dispose();
                    for (int i = 0; i < images.Count; i++)
                    {
                        File.Delete(imageFilePath + i + ".png");
                    }
                }
                return true;
            }
            catch (Exception e)
            {

                throw e;
            }


        }



        /// <summary>
        /// 文本保存为其他格式的文件，可以是2003、2007的word，也可以是图片等
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <param name="saveFormat">文档类型</param>
        /// Member Name Description Value 
        /// None Default, invalid value for file format.  0 
        /// Doc Saves the document in the Microsoft Word 97 - 2003 Document format.  1 
        /// Docx Saves the document as a Microsoft Office 2007 Open XML Document (macro-free).  8 
        /// Docm Saves the document as a Microsoft Office 2007 Open XML Macro-Enabled Document.  9 
        /// FlatOpc Saves the document as a Microsoft Word 2007 XML Document.  10 
        /// Rtf Saves the document in the RTF format. All characters above 7-bits are escaped as hexadecimal or Unicode characters.  6 
        /// WordML Saves the document in the Microsoft Word 2003 WordprocessingML format.  5 
        /// Pdf Saves the document as PDF directly (without going through Aspose.Pdf).  15 
        /// Xps Saves the document in the XPS (XML Paper Specification) format.  16 
        /// Html Saves the document in the HTML format.  4 
        /// Mhtml Saves the document in the MHTML (Web archive) format.  13 
        /// Text Saves the document in the plain text format.  2 
        /// Odt Saves the document in the OpenDocument format.  12 
        /// Epub Saves the document in the IDPF EPUB format.  14 
        /// Xaml Beta. Saves the document in the Extensible Application Markup Language (XAML) format.  20 
        /// AsposePdf Saves the document in the Aspose.Pdf.Xml format that can be read by Aspose.Pdf to produce a PDF file. This is the legacy approach that will be later deprecated.  3 
        /// <returns>保存成功返回True</returns>
        /// 

        public string splitPara(string param)
        {
            string Para = "";
            switch (param)
            {

                case "PM2.5":
                    Para = "PM";
                    break;
                case "PM10":
                    Para = "PM";
                    break;
                case "O3":
                    Para = "O";
                    break;
                case "CO":
                    Para = "CO";
                    break;
                case "SO2":
                    Para = "SO";
                    break;
                case "NO2":
                    Para = "NO";
                    break;


            }
            return Para;
        }
        public string splitParaSub(string param)
        {
            string Para = "";
            switch (param)
            {

                case "PM2.5":
                    Para = "2.5";
                    break;
                case "PM10":
                    Para = "10";
                    break;
                case "O3":
                    Para = "3";
                    break;
                case "CO":
                    Para = "";
                    break;
                case "SO2":
                    Para = "2";
                    break;
                case "NO2":
                    Para = "2";
                    break;


            }
            return Para;
        }
        public string exportWord(string wordPartContent, string userName, string datetime)
        {

            string[] parts = wordPartContent.Split(';');
            string WordModelPath = ConfigurationManager.AppSettings["WordModelPath"].ToString();
            string strTextBasePath = ConfigurationManager.AppSettings["exportWordPath"].ToString();
            string localbasepath = string.Format(strTextBasePath + "word/");
            string strDate = DateTime.Now.ToString("yyyy");
            Aspose.Words.Document doc = new Aspose.Words.Document("H:\\wordModel.docx");
            DocumentBuilder docBuilder = new DocumentBuilder(doc);

            for (int i = 0; i < 10; i++)
            {
                string[] partLine = parts[i].Split('#');
                docBuilder.MoveToBookmark("date" + i.ToString());
                docBuilder.Write((partLine[0].Split(':'))[1]);
                string v1 = "";
                string v2 = "";

                if (!string.IsNullOrEmpty(partLine[1]))
                    v1 = partLine[1];

                Aspose.Words.Bookmark bookmark = doc.Range.Bookmarks["para" + i.ToString()];
                bookmark.Text = v1;
                Aspose.Words.Bookmark bookmark2 = doc.Range.Bookmarks["sub" + i.ToString()];
                bookmark2.Text = v2;

                docBuilder.MoveToBookmark("AQI" + i.ToString());
                docBuilder.Write(partLine[2]);
            }

            docBuilder.MoveToBookmark("Name");
            docBuilder.Write(userName);
            docBuilder.MoveToBookmark("bottomDate");
            docBuilder.Write(datetime);
            string WordInfo = localbasepath + "上海市10天空气质量预报_" + DateTime.Parse(datetime).ToString("yyyyMMdd") + ".docx";
            doc.Save(localbasepath + "上海市10天空气质量预报_" + DateTime.Parse(datetime).ToString("yyyyMMdd") + ".docx", Aspose.Words.SaveFormat.Docx);
            return WordInfo;

        }

        public bool  SaveAs(string fileName, SaveFormat saveFormat)
        {

            try
            {
                m_Doc.Save(fileName, saveFormat);
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        #endregion

        #region 填充书签
        /// <summary>  
        /// 填充书签  
        /// </summary>  
        /// <param name="bookmark">书签</param>  
        /// <param name="value">值</param>  
        public void ReplacePast(string bookmark, string value)
        {
            try
            {
                Bookmark bkObj = m_Doc.Range.Bookmarks[bookmark];
                
                if (bkObj != null)
                {
                    bkObj.Text = value;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void Replace(string bookmark, string value)
        {
            try
            {
                Bookmark bkObj = m_Doc.Range.Bookmarks[bookmark];                
                if (bkObj != null)
                {
                    bkObj.Text = value;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        #endregion
        #region 填充书签
        /// <summary>  
        /// 填充书签  
        /// </summary>  
        /// <param name="bookmark">书签</param>  
        /// <param name="value">值</param>  
        public string  Getbookmark(string bookmark)
        {
            string value = "";
            try
            {
                Bookmark bkObj = m_Doc.Range.Bookmarks[bookmark];
                if (bkObj != null)
                {
                    value=bkObj.Text;
                }               
            }
            catch (Exception ex)
            {
               
                throw ex;
               
            }
            return value;
        }
        #endregion


        #region//插入图片

        public void InsertPic(string bookmark, string fileName, float width, float height)
        {
            try
            {
                Bookmark bkObj = m_Doc.Range.Bookmarks[bookmark];
                if (bkObj != null)
                {
                    m_Builder.MoveToBookmark(bookmark);
                    // By default, the image is inserted at 100% scale.
                    Shape shape = m_Builder.InsertImage(fileName, width, height);

                    // It is easy to change the shape size. In this case, make it 50% relative to the current shape size.
                    //shape.Width = width;
                    //shape.Height = height;
                    //m_Builder.InsertImage(

                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        public void InsertPic(string bookmark, byte[] bytes, float width, float height)
        {
            try
            {
                if (width == 0)
                {
                }
                else
                {
                    Bookmark bkObj = m_Doc.Range.Bookmarks[bookmark];
                    if (bkObj != null)
                    {
                        m_Builder.MoveToBookmark(bookmark);
                        // By default, the image is inserted at 100% scale.
                        Shape shape = m_Builder.InsertImage(bytes, width, height);

                        // It is easy to change the shape size. In this case, make it 50% relative to the current shape size.
                        //shape.Width = width;
                        //shape.Height = height;
                        //m_Builder.InsertImage(

                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void InsertPic(string bookmark, Stream stream, float width, float height)
        {
            try
            {
                Bookmark bkObj = m_Doc.Range.Bookmarks[bookmark];
                if (bkObj != null)
                {
                    m_Builder.MoveToBookmark(bookmark);
                    // By default, the image is inserted at 100% scale.
                    Shape shape = m_Builder.InsertImage(stream, width, height);

                    // It is easy to change the shape size. In this case, make it 50% relative to the current shape size.
                    //shape.Width = width;
                    //shape.Height = height;
                    //m_Builder.InsertImage(

                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        public void InsertPicTwo(string bookmark1, string bookmark2, float width, float height)
        {
            Bookmark bkObj = m_Doc.Range.Bookmarks[bookmark1];
            m_Builder.MoveToBookmark(bookmark1);
            // Shape shape = m_Builder.InsertImage(fileName, width, height);

        }



        #endregion


        #region 填充表格
        /// <summary>
        /// 根据已经存在的表格模板，实现表格行的添加，并把存在tbBody中的信息进行填充
        /// </summary>
        /// <param name="tableIndex">表格在word模板中的需要</param>
        /// <param name="tbBody">DataTable类型的表格数据</param>
        public void AppendTable(int tableIndex, DataTable tbBody)
        {
            Table table = (Table)m_Doc.GetChild(NodeType.Table, tableIndex, true);

            Row clonedRow = (Row)table.LastRow.Clone(true);
            // Remove all content from the cloned row's cells. This makes the row ready for
            // new content to be inserted into.
            foreach (Cell cell in clonedRow.Cells)
            {
                Run nc = (Run)cell.GetChild(NodeType.Run, 0, true);
                if (nc != null)
                    nc.Text = "";
            }

            //根据Datatable追加行
            for (int i = 0; i < tbBody.Rows.Count; i++)
            {
                // Add the row to the end of the table.
                table.AppendChild(clonedRow.Clone(true));
                for (int j = 0; j < clonedRow.Cells.Count; j++)
                {
                    m_Builder.MoveToCell(tableIndex, table.Rows.Count - 1, j, 0);
                    m_Builder.Write(tbBody.Rows[i][j].ToString());
                }
            }
        }
        #endregion


        #region Misc
        /// <summary>
        /// 在指定书签位置，插入规定行数、列数的表格
        /// </summary>
        /// <param name="bookmark">书签</param>
        /// <param name="rows">行数</param>
        /// <param name="columns">列数</param>
        /// <returns>返回表格</returns>
        //public Table InsertTable(string bookmark, int rows, int columns)
        //{
        //    try
        //    {
        //        //object miss = System.Reflection.Missing.Value;
        //        //object oStart = bookmark;
        //        //Word.Range range = wApp.Application.ActiveDocument.Bookmarks.get_Item(ref oStart).Range;//出错
        //        //Word.Table newTable = wApp.Application.ActiveDocument.Tables.Add(range, rows, columns, ref miss, ref miss);
        //        //newTable.Borders.Enable = 1;
        //        //newTable.Borders.OutsideLineWidth = Word.WdLineWidth.wdLineWidth025pt;
        //        //newTable.PreferredWidthType = Word.WdPreferredWidthType.wdPreferredWidthPercent;
        //        //newTable.PreferredWidth = 100; ;
        //        //newTable.AllowPageBreaks = false;
        //        //wApp.Application.Selection.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;//水平居中
        //        ////wApp.Application.Selection.Cells.VerticalAlignment = Word.WdCellVerticalAlignment.wdCellAlignVerticalCenter;//垂直居中
        //        //return newTable;
        //    }
        //    catch (Exception ex)
        //    {
        //        throw ex;

        //    }
        //}


        /// <summary>
        /// 通过序号获取所在的表格
        /// </summary>
        /// <param name="index">序号</param>
        /// <returns></returns>
        public Table GetTable(int tableIndex)
        {
            Table table = (Table)m_Doc.GetChild(NodeType.Table, tableIndex, true);
            return table;
        }


        /// <summary>
        /// 向表格插入行
        /// </summary>
        /// <param name="index">表格序号</param>
        /// <param name="Rows">行数</param>
        public void AddRows(int tableIndex, int rows)
        {
            Table table = (Table)m_Doc.GetChild(NodeType.Table, tableIndex, true);
            Row clonedRow = (Row)table.LastRow.Clone(true);
            // Remove all content from the cloned row's cells. This makes the row ready for
            // new content to be inserted into.
            foreach (Cell cell in clonedRow.Cells)
            {
                Run nc = (Run)cell.GetChild(NodeType.Run, 0, true);
                if (nc != null)
                    nc.Text = "";
            }

            //根据Datatable追加行
            for (int i = 0; i < rows; i++)
            {
                // Add the row to the end of the table.
                table.AppendChild(clonedRow.Clone(true));
            }
        }

        /// <summary>
        /// 设置某个单元格的值
        /// </summary>
        /// <param name="table">表</param>
        /// <param name="Row">行</param>
        /// <param name="Col">列</param>
        /// <param name="Value">值</param>
        public void SetCellValue(int tableIndex, int row, int col, string value)
        {
            m_Builder.MoveToCell(tableIndex, row, col, 0);

            m_Builder.Write(value);
        }




       
        #endregion
    }

}
