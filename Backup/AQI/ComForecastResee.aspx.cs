﻿using System;
using System.Collections;
using System.Configuration;
using System.Data;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using Readearth.Data;
using System.Text;

public partial class AQI_ComForecastResee : System.Web.UI.Page
{
    public string m_ForecastDate;
    public string m_FirstTab;
    public string m_UserJson;
    public string m_PeopleJson;
    private Database m_Database;
    public bool m_UnLogin;
    public string Limits;
    public string LoginName = "";
    public string userName;
    protected void Page_Load(object sender, EventArgs e)
    {
        Limits = Request.Cookies["User"]["indexUser"];
        userName = Request.Cookies["User"]["name"];
        if (!Page.IsPostBack)
        {
            DateTime dtNow = DateTime.Now;
            m_Database = new Database();
            string strSQL = "SELECT UserName,Alias FROM T_User WHERE UserName='" + userName + "'";
            DataTable dt = m_Database.GetDataTable(strSQL);
            if (dt.Rows.Count > 0)
                LoginName = dt.Rows[0][1].ToString();
            m_ForecastDate = dtNow.AddDays(-1).ToString("yyyy年MM月dd日");
            int backDays = int.Parse(ConfigurationManager.AppSettings["BackDays"]);
            int LimitingTime = int.Parse(ConfigurationManager.AppSettings["LimitingTime"]);
            DateTime dateTimeString = dtNow.Date.AddHours(LimitingTime);
            H00.Value = m_ForecastDate;
            H00.Attributes.Add("tag", backDays.ToString());
            H00.Attributes.Add("todayDateTime", dateTimeString.ToString("yyyy-MM-dd HH:mm:ss"));
            CreateTable(dtNow, backDays);
            //CreateUsers();
            //CreatePeople();
        }

    }
    private void CreateTable(DateTime dtNow, int backDays)
    {
        Database db = m_Database;

        string strSQL = "SELECT DM FROM D_DurationTest WHERE CODE = '1'";
        strSQL = strSQL + ";SELECT DM,MC FROM D_ITEM WHERE DM>0";
        //strSQL = strSQL + ";SELECT DM FROM D_DATATYPE";
        DataSet dtDicTables = db.GetDataset(strSQL);

        DataTable dicDuration = dtDicTables.Tables[0];
        DataTable dicItem = dtDicTables.Tables[1];
        //DataTable dicItem = dtDicTables.Tables[2];

        StringBuilder sb = new StringBuilder();
        //创建预报污染物标签
        for (int i = 0; i < dicItem.Rows.Count; i++)
        {
            DataRow row = dicItem.Rows[i];
            if (i == 0)
            {
                sb.Append(string.Format("<li class='liTab'><span id=\"{0}_{1}\" class = \"tabHighlight\">{0}</span></li>", row[1], row[0]));
                m_FirstTab = string.Format("{0}_{1}", row[1], row[0]);
            }
            else
                sb.Append(string.Format("<li class='liTab'><span id=\"{0}_{1}\"><a href=\"javascript:tabClick('{0}_{1}')\">{0}</a></span></li>", row[1], row[0]));
        }

        tabItem.InnerHtml = sb.ToString();
        sb.Length = 0;

        string[] classes = { "tablerowNew", "tablerow2New" };
        string[] classesQi = { "tablerowQixiang", "tablerow2QiX" };

        DateTime dtRow = dtNow;
        HtmlTableRow newRow = null;
        HtmlTableCell td = null;
        int index = 0;
        int duration = 0;
        string[] duraionCell = { "日期", "0时—6时", "6时—12时", "12时—20时", "20时—0时", "20时—6时", "日平均" };
        string[] dataType = { "实测", "综合预报" };
        newRow = new HtmlTableRow();
        for (int i = 0; i < 7; i++)
        {
            td = new HtmlTableCell();
            if (i == 0)
            {
                td.Attributes.Add("rowspan", "2");
            }
            else
            {
                td.Attributes.Add("colspan", "2");
            }
            //if (Limits == "2")
            //{
            //    if (i == 1 || i == 4)
            //    {
            //        td.Attributes.Add("class", "hidden");
            //    }
            //    else
            //    {
            //        td.Attributes.Add("class", "tabletitleQixiang");
            //    }
            //}
            //else
            //{
                td.Attributes.Add("class", "tabletitleNew");
            //}
            td.InnerHtml = duraionCell[i];
            newRow.Cells.Add(td);
        }
        forecastTable.Rows.Insert(0, newRow);

        newRow = new HtmlTableRow();
        for (int i = 0; i < 12; i++)
        {
            td = new HtmlTableCell();
            //if (Limits == "2")
            //{
            //    if (i == 0 || i == 1 || i == 6 || i == 7)
            //    {
            //        td.Attributes.Add("class", "hidden");
            //    }
            //    else
            //    {
            //        td.Attributes.Add("class", "tabletitleQixiang");
            //    }
            //}
            //else
            //{
                td.Attributes.Add("class", "tabletitleNew");
            //}
            if (i % 2 == 0)
            {
                td.InnerHtml = dataType[0];
            }
            else
            {
                td.InnerHtml = dataType[1];
            }
            newRow.Cells.Add(td);
        }
        forecastTable.Rows.Insert(1, newRow);

        //创建历史预报信息表格
        for (int i = 0; i <= backDays; i++)
        {
            dtRow = dtNow.AddDays(i - backDays);

            newRow = new HtmlTableRow();
            td = new HtmlTableCell();

            index = 0;
            //if (Limits == "2")
            //    td.Attributes.Add("class", "letTablerowQixiang");
            //else
                td.Attributes.Add("class", "letTablerowNew");


            td.InnerHtml = string.Format("<span id ='td{0}1'></span><input id='td{0}1Buddy' type='text' value='{1}' class ='hiddenText'  onchange='hiddenTextChanged(this)' />", i, dtRow.ToString("yyyy-MM-dd"));
            HtmlImage icoImage = new HtmlImage();
            icoImage.Src = "images/arrow.png";
            icoImage.Attributes.Add("class", "ico");
            icoImage.Attributes.Add("onclick", "WdatePicker({el:'td" + i.ToString() + "1Buddy',dateFmt:'yyyy-MM-dd'})");
            td.Controls.Add(icoImage);
            newRow.Cells.Add(td);

            foreach (DataRow row in dicDuration.Rows)
            {
                duration = int.Parse(row[0].ToString());
                //实况（0）和综合预报（1）
                for (int j = 0; j < 2; j++)
                {
                    td = new HtmlTableCell();
                    if (index == 0)
                        index = 1;
                    else
                        index = 0;
                    //if (Limits == "2")
                    //{
                    //    if (duration == 1 || duration == 4)
                    //    {
                    //        td.Attributes.Add("class", "hidden");

                    //    }
                    //    else
                    //    {
                    //        td.Attributes.Add("class", classesQi[index]);
                    //    }
                    //}
                    //else
                    //{
                        td.Attributes.Add("class", classes[index]);
                    //}

                    foreach (DataRow r in dicItem.Rows)
                    {
                        if (m_FirstTab == string.Format("{0}_{1}", r[1], r[0]))
                            sb.Append(string.Format("<div id = 'H{0}{1}{2}{3}' class = 'show'></div>", i, j, row[0], r[0]));
                        else
                            sb.Append(string.Format("<div id = 'H{0}{1}{2}{3}' class = 'hidden'></div>", i, j, row[0], r[0]));
                    }
                    td.InnerHtml = sb.ToString();
                    sb.Length = 0;
                    newRow.Cells.Add(td);
                }
            }
            forecastTable.Rows.Insert(i + 2, newRow);
        }

        newRow = new HtmlTableRow();
        string[] modualType = { "综合预报", "数值预报" };
        for (int i = 0; i < 13; i++)
        {
            td = new HtmlTableCell();
            //if (Limits == "2")
            //{
            //    if (i == 1 || i == 2 || i == 7 || i == 8)
            //    {
            //        td.Attributes.Add("class", "hidden");
            //    }
            //    else
            //    {
            //        td.Attributes.Add("class", "tabletitleQixiang");
            //    }
            //}
            //else
            //{
                td.Attributes.Add("class", "tabletitleNew");
            //}
            if (i == 0)
            {
                td.InnerHtml = "&nbsp;";
            }
            else if (i % 2 == 0)
            {
                //if (Limits == "2")
                //{
                //    td.InnerHtml = "&nbsp;";
                //}
                //else
                //{
                    td.InnerHtml = modualType[1];
                //}
            }
            else
            {
                td.InnerHtml = modualType[0];
            }
            newRow.Cells.Add(td);
        }
        forecastTable.Rows.Insert(5, newRow);

        //创建实时预报，下午3点起报，预报48小时
        int startInputIndex = 0;//第7个单元格开始输入数据
        string className = string.Empty;
        string onClick = string.Empty;
        for (int i = backDays + 1; i <= backDays + 3; i++)
        {

            newRow = new HtmlTableRow();
            td = new HtmlTableCell();

            index = 0;
            //if (Limits == "2")
            //    td.Attributes.Add("class", "letTablerowQixiang");
            //else
                td.Attributes.Add("class", "letTablerowNew");
            td.InnerHtml = string.Format("<span id ='td{0}1'></span>", i);
            newRow.Cells.Add(td);

            foreach (DataRow row in dicDuration.Rows)
            {
                duration = int.Parse(row[0].ToString());
                //综合预报（1）和数值预报（2）
                for (int j = 1; j < 3; j++)
                {
                    td = new HtmlTableCell();
                    if (index == 0)
                        index = 1;
                    else
                        index = 0;

                    startInputIndex += 1;
                    //添加文本框的条件，需要是综合预报，且第7个开始，每隔一个共48小时，另外日平均和不需要输入
                    //if ((startInputIndex >= 7 && startInputIndex < 31 && (startInputIndex - 7) % 2 == 0 && j == 1 && startInputIndex % 12 != 11))
                    //    onClick = "onclick = 'showInput(event,this)'";
                    //else
                    //    onClick = "";
                    //if (Limits == "2")
                    //{
                    //    if (duration == 1 || duration == 4)
                    //    {
                    //        td.Attributes.Add("class", "hidden");

                    //    }
                    //    else
                    //    {
                    //        td.Attributes.Add("class", classesQi[index]);
                    //    }
                    //}
                    //else
                    //{
                        td.Attributes.Add("class", classes[index]);
                    //}
                    foreach (DataRow r in dicItem.Rows)
                    {


                        if (m_FirstTab == string.Format("{0}_{1}", r[1], r[0]))
                            className = "show";
                        else
                            className = "hidden";
                        if (onClick != "")
                            className = "divInputType " + className;
                        sb.Append(string.Format("<div id = 'H{0}{1}{2}{3}'  class = '{4}'" + onClick + "></div>", i, j, row[0], r[0], className));

                    }
                    td.InnerHtml = sb.ToString();
                    sb.Length = 0;
                    newRow.Cells.Add(td);
                }
            }
            forecastTable.Rows.Insert(forecastTable.Rows.Count, newRow);



            dtRow = dtRow.AddDays(1);

        }

    }

    /// <summary>
    /// 在前台创建用户列表json对象
    /// </summary>
    ////private void CreateUsers()
    ////{
    ////    string strSQL = "SELECT FldValue,FldKey FROM V_DictionaryValues WHERE FldKey = 102 OR FldKey = 201 OR FldKey = 202 ORDER BY FldKey,FldValue ";

    ////    DataTable tbUsers = m_Database.GetDataTable(strSQL);
    ////    StringBuilder sb = new StringBuilder();
    ////    string fldKey = "";

    ////    foreach (DataRow row in tbUsers.Rows)
    ////    {
    ////        if (row[1].ToString() != fldKey)
    ////            sb.AppendFormat("',{0}:'{1}", row[1], row[0]);
    ////        else
    ////            sb.AppendFormat("|{0}", row[0]);

    ////        fldKey = row[1].ToString();
    ////    }

    ////    if (sb.Length > 0)
    ////    {
    ////        sb.Remove(0, 2);
    ////        sb.Insert(0, "{");
    ////        sb.Append("'}");
    ////    }

    ////    m_UserJson = sb.ToString();
    ////}
    //private void CreatePeople()
    //{
    //    string strSQL = "SELECT Phone,Name FROM T_Phones ORDER BY Name";
    //    DataTable tbUsers = m_Database.GetDataTable(strSQL);
    //    StringBuilder sb = new StringBuilder();
    //    for (int a = 0; a < tbUsers.Rows.Count; a++)
    //    {
    //        DataRow row = tbUsers.Rows[a];
    //        string m = "";
    //        if (row[1].ToString().Length == 2)
    //            m = row[1].ToString().Substring(0, 1) + "&nbsp&nbsp" + row[1].ToString().Substring(1, 1);
    //        else
    //            m = row[1].ToString();
    //        if (a == tbUsers.Rows.Count - 1)
    //            sb.Append(string.Format("{0}:{1}", row[0].ToString(), m));
    //        else
    //            sb.Append(string.Format("{0}:{1}", row[0].ToString(), m) + ",");
    //    }
    //    m_PeopleJson = sb.ToString();
    //}
}