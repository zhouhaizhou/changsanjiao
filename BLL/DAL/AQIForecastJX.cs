﻿#region The ComForecast Copyright & Version History
/*
 * ============================================================== 
 * 
 * ComForecast, Version 1.0
 * 
 * Copyright (c) 2013-2014 上海地听信息科技有限公司.  版权所有.
 * 
 * 张伟锋
 * 
 * 修改：
 *       
 * 张伟锋              2010年11月25日
 * ====================================================================
 * 
 * 功能说明：用户实现环境监测中心的综合预报业务，包括预报单的录入，历史预报信息的调取等。
 *
 */
#endregion
using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using Readearth.Data;
using Readearth.Data.Entity;
using MMShareBLL.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ChinaAQI;
using Lucas.AQI2012;
using MASlib;
using Lucas;
using System.Net;
using Readearth.Common;
using WeiBo;
using Aspose.Cells;
using System.IO;
using System.Text.RegularExpressions;
using MMShareBLL.DAL;
using Aspose.Words;
using Aspose.Words.Tables;
using System.Web;
using System.Drawing;
using System.Security.Cryptography;

namespace MMShareBLL.DAL
{
    public class AQIForecastJX
    {
        //用于记录系统错误日志
        protected static readonly log4net.ILog m_Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private Database m_Database;
        //江西的数据库
        private Database m_DatabaseJX;

        private int m_BackDays;
        public AQIForecastJX()
        {
            m_Database = new Database();
            m_DatabaseJX = new Database("JXDBCONFIG");
            m_BackDays = int.Parse(ConfigurationManager.AppSettings["BackDays"]);
        }
        /// <summary>
        /// 根据当前传入的时间，获取综合预报的起报时间
        /// </summary>
        /// <param name="hour"></param>
        /// <returns></returns>
        public DateTime GetManualForecastDate(string hour)
        {
            DateTime dtNow = DateTime.Now.Date.AddHours(18);
            if (hour != "")
                dtNow = DateTime.Parse(hour).AddHours(18);
            return dtNow;
        }

        /// <summary>
        /// 返回臭氧前驱体的数据
        /// </summary>
        /// <param name="fromDate">开始日期</param>
        /// <param name="toDate">结束日期</param>
        /// <param name="SiteID">站点编号</param>
        /// <returns></returns>
        public string GetOzoneChart(string fromDate, string toDate, string siteID)
        {
            string strSQL = "";
            string strReturn = "";
            string x = "";
            string y = "";
            double minX = 1000000000000;
            string from = DateTime.Parse(fromDate).ToString("yyyy-MM-dd HH:mm:ss");
            string to = DateTime.Parse(toDate).AddDays(1).ToString("yyyy-MM-dd HH:mm:ss");
            string strWhere = " [LST] BETWEEN '" + from + "' AND '" + to + "' AND SiteID='" + siteID + "' AND durationid=10";
            string dateFiled = " DATEDIFF(S,'1970-01-01 00:00:00', [LST]) AS [LST] ";
            //212：烷烃 213：烯烃 214：芳香烃 215：总VOC  8：臭氧
            strSQL = "SELECT " + dateFiled + " ,VALUE from DMS_DATA WHERE parameterid =213 AND " + strWhere + " ORDER BY [LST] ";
            strSQL = strSQL + ";SELECT " + dateFiled + ",VALUE from DMS_DATA WHERE parameterid =214 AND " + strWhere + " ORDER BY [LST] ";
            strSQL = strSQL + ";SELECT " + dateFiled + ",VALUE from DMS_DATA WHERE parameterid =215 AND " + strWhere + " ORDER BY [LST] ";
            strSQL = strSQL + ";SELECT " + dateFiled + ",VALUE from DMS_DATA WHERE parameterid =216 AND " + strWhere + " ORDER BY [LST] ";
            strSQL = strSQL + ";SELECT " + dateFiled + ",VALUE*1000 from DMS_DATA WHERE parameterid =8 AND " + strWhere + " ORDER BY [LST] ";
            try
            {
                DataSet ds = m_Database.GetDataset(strSQL);
                for (int index = 0; index < ds.Tables.Count; index++)
                {
                    DataTable dtElement = ds.Tables[index];
                    x = ""; y = "";
                    foreach (DataRow dr in dtElement.Rows)
                    {
                        x = x + "|" + dr[0].ToString();
                        if (double.Parse(dr[0].ToString()) < minX)
                            minX = double.Parse(dr[0].ToString());
                        y = y + "|" + dr[1].ToString();
                    }
                    strReturn = strReturn + ",'" + index.ToString() + "':'" + x.TrimStart('|') + "*" + y.TrimStart('|') + "'";
                }
                if (strReturn != ",")
                    strReturn = "{" + strReturn.TrimStart(',') + ",minX:" + minX.ToString() + "}";
                return strReturn;
            }
            catch (Exception ex)
            {
                m_Log.Error("GetOzoneChart", ex);
                return ex.ToString();
            }
        }

        /// <summary>
        /// 返回气溶胶曲线数据
        /// </summary>
        /// <param name="fromDate"></param>
        /// <param name="toDate"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public string GetAQIChart(string fromDate, string toDate, string station)
        {
            string strSQL = "";
            string strReturn = "";
            string x = "";
            string y = "";

            string from = DateTime.Parse(fromDate).ToString("yyyy-MM-dd HH:mm:ss");
            string to = DateTime.Parse(toDate).AddDays(1).ToString("yyyy-MM-dd HH:mm:ss");


            //新的方法 现在需要禁掉，如果以后要可以启用
            //string strWhere = " [END] BETWEEN '" + DateTime.Parse(fromDate).AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss") + "' AND '" + DateTime.Parse(toDate).AddDays(1).AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss") + "' AND STATION='" + station + "'";
            //string dateFiled = " DATEDIFF(S,'1970-01-01 00:00:00', DATEADD(hour,1,[END])) AS [END] ";

            //string dateFileAdd = "  DATEDIFF(S,'1970-01-01 00:00:00',DATEADD(hour,1,LST)) AS LST ";
            //string timeWhere = " LST BETWEEN '" + DateTime.Parse(fromDate).AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss") + "' AND '" + DateTime.Parse(toDate).AddDays(1).AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss") + "'";

            //END  变成  Start  上面代码如果启用也要改过来
            string strWhere = " [Start] BETWEEN '" + from + "' AND '" + to + "' AND STATION='" + station + "'";
            string dateFiled = " DATEDIFF(S,'1970-01-01 00:00:00', [Start]) AS [END] ";

            string dateFileAdd = "  DATEDIFF(S,'1970-01-01 00:00:00', LST) AS LST ";
            string timeWhere = " LST BETWEEN '" + from + "' AND '" + to + "'";


            //Cl,NO3,SO4,Na,NH4,K,Mg,Ca,[PM2#5(ug/m3)]
            // if (station != "NJ")
            //{
            strSQL = "SELECT " + dateFiled + " ,Cl from T_AirPercent WHERE [Cl-quality]='V' AND " + strWhere + " ORDER BY [END] ";
            strSQL = strSQL + ";SELECT " + dateFiled + ",NO3 from T_AirPercent WHERE [NO3-quality]='V' AND " + strWhere + " ORDER BY [END] ";
            strSQL = strSQL + ";SELECT " + dateFiled + ",SO4 from T_AirPercent WHERE [SO4-quality]='V' AND " + strWhere + " ORDER BY [END] ";
            strSQL = strSQL + ";SELECT " + dateFiled + ",Na from T_AirPercent WHERE [Na-quality]='V' AND " + strWhere + " ORDER BY [END] ";
            strSQL = strSQL + ";SELECT " + dateFiled + ",NH4 from T_AirPercent WHERE [NH4-quality]='V' AND " + strWhere + " ORDER BY [END] ";
            strSQL = strSQL + ";SELECT " + dateFiled + ",K from T_AirPercent WHERE [K-quality]='V' AND " + strWhere + " ORDER BY [END] ";
            strSQL = strSQL + ";SELECT " + dateFiled + ",Mg from T_AirPercent WHERE [Mg-quality]='V' AND " + strWhere + " ORDER BY [END] ";
            strSQL = strSQL + ";SELECT " + dateFiled + ",Ca from T_AirPercent WHERE [Ca-quality]='V' AND " + strWhere + " ORDER BY [END] ";
            // }
            //else {
            //    strSQL = "SELECT '1970-01-01 00:00:00' ,-1  ";
            //    strSQL = strSQL + ";SELECT '1970-01-01 00:00:00',-1  ";
            //    strSQL = strSQL + ";SELECT '1970-01-01 00:00:00',-1  ";
            //    strSQL = strSQL + ";SELECT '1970-01-01 00:00:00',-1  ";
            //    strSQL = strSQL + ";SELECT '1970-01-01 00:00:00',-1  ";
            //    strSQL = strSQL + ";SELECT '1970-01-01 00:00:00',-1  ";
            //    strSQL = strSQL + ";SELECT '1970-01-01 00:00:00',-1  ";
            //    strSQL = strSQL + ";SELECT '1970-01-01 00:00:00',-1  ";
            //}
            string str = "";
            //56 OC(热学)  57 EC(热学) 58 OC(光学) 59 EC(光学) 228 浦东 浦东-崇明 271-青浦,OC需要用OC*1.4表示    张伟锋    2014-08-10
            if (station == "CM")
            {
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",(Value*1.4) AS Value  from DMS_DATA WHERE QCCode<>'9' and ParameterID=56  AND SiteID=228  AND DurationID=10  AND " + timeWhere + "  ORDER BY LST ";
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",Value  from DMS_DATA WHERE QCCode<>'9' and ParameterID=57  AND SiteID=228  AND DurationID=10  AND " + timeWhere + "  ORDER BY LST ";
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",(Value*1.4) AS Value  from DMS_DATA WHERE QCCode<>'9' and ParameterID=58  AND SiteID=228  AND DurationID=10  AND " + timeWhere + "  ORDER BY LST ";
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",Value  from DMS_DATA WHERE QCCode<>'9' and ParameterID=59  AND SiteID=228  AND DurationID=10  AND " + timeWhere + " ORDER BY LST ";
                str = "SELECT DATEDIFF(S,'1970-01-01 00:00:00', LST) AS [END] ,ROUND(Value*1000,1) as Value from Data_RT_Site WHERE LST BETWEEN '" + from + "' AND '" + to + "' AND  SiteID=249 AND AQIItemID=101 ORDER BY LST ";
            }
            if (station == "QP")
            {
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",(Value*1.4) AS Value  from DMS_DATA WHERE QCCode<>'9' and ParameterID=56  AND SiteID=271  AND DurationID=10   AND " + timeWhere + "  ORDER BY LST ";
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",Value  from DMS_DATA WHERE QCCode<>'9' and ParameterID=57  AND SiteID=271  AND DurationID=10   AND " + timeWhere + "  ORDER BY LST ";
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",Value  from DMS_DATA WHERE QCCode<>'9' and ParameterID=58  AND SiteID=271  AND DurationID=10   AND " + timeWhere + "  ORDER BY LST ";
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",(Value*1.4) AS Value  from DMS_DATA WHERE QCCode<>'9' and ParameterID=59  AND SiteID=271  AND DurationID=10   AND " + timeWhere + "  ORDER BY LST ";
                str = "SELECT DATEDIFF(S,'1970-01-01 00:00:00', LST) AS [END] ,ROUND(Value*1000,1) as Value from Data_RT_Site WHERE LST BETWEEN '" + from + "' AND '" + to + "' AND  SiteID=203 AND AQIItemID=101 ORDER BY LST ";
            }
            if (station == "NJ")
            {
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",(Value*1.4) AS Value  from T_OCECData WHERE ParameterID=56  AND SiteID=3110  AND DurationID=10   AND " + timeWhere + "  ORDER BY LST ";
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",Value  from T_OCECData WHERE ParameterID=57  AND SiteID=3110  AND DurationID=10   AND " + timeWhere + "  ORDER BY LST ";
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",Value  from T_OCECData WHERE ParameterID=58  AND SiteID=3110  AND DurationID=10   AND " + timeWhere + "  ORDER BY LST ";
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",(Value*1.4) AS Value  from T_OCECData WHERE ParameterID=59  AND SiteID=3110  AND DurationID=10   AND " + timeWhere + "  ORDER BY LST ";
                //str = "SELECT DATEDIFF(S,'1970-01-01 00:00:00', LST) AS [END] ,ROUND(Value*1000,1) as Value from Data_RT_Site WHERE LST BETWEEN '" + from + "' AND '" + to + "' AND  SiteID=203 AND AQIItemID=101 ORDER BY LST ";
                str = "SELECT DATEDIFF(S,'1970-01-01 00:00:00', TimePoint) AS [END] ,PM2_5 from [semc_dmc].[dbo].[China_RT_CNEMC_Data] WHERE TimePoint BETWEEN '" + from + "' AND '" + to + "' AND  Area = '南京' AND PositionName ='草场门' ORDER BY TimePoint ";
            }
            if (station == "PD")
            {
                //create by 薛辉 on 2014-08-14
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",(Value*1.4) AS Value  from DMS_DATA WHERE QCCode<>'9' and ParameterID=56  AND SiteID=228  AND DurationID=10   AND " + timeWhere + "  ORDER BY LST ";
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",Value  from DMS_DATA WHERE QCCode<>'9' and ParameterID=57  AND SiteID=228  AND DurationID=10   AND " + timeWhere + "  ORDER BY LST ";
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",Value  from DMS_DATA WHERE QCCode<>'9' and ParameterID=58  AND SiteID=228  AND DurationID=10   AND " + timeWhere + "  ORDER BY LST ";
                strSQL = strSQL + ";SELECT " + dateFileAdd + ",(Value*1.4) AS Value  from DMS_DATA WHERE QCCode<>'9' and ParameterID=59  AND SiteID=228  AND DurationID=10   AND " + timeWhere + "  ORDER BY LST ";
                str = "SELECT DATEDIFF(S,'1970-01-01 00:00:00', LST) AS [END] ,ROUND(Value*1000,1) as Value from Data_RT_Site WHERE LST BETWEEN '" + from + "' AND '" + to + "' AND  SiteID=228 AND AQIItemID=101 ORDER BY LST ";
            }
            Database m_DatabaseAQI = new Database("AQIWEB");
            DataTable dt = m_DatabaseAQI.GetDataTable(str);
            try
            {
                DataSet ds = m_Database.GetDataset(strSQL);
                ds.Tables.Add(dt);
                for (int index = 0; index < ds.Tables.Count; index++)
                {
                    DataTable dtElement = ds.Tables[index];
                    x = ""; y = "";
                    foreach (DataRow dr in dtElement.Rows)
                    {
                        x = x + "|" + dr[0].ToString();
                        y = y + "|" + dr[1].ToString();
                    }
                    strReturn = strReturn + ",'" + index.ToString() + "':'" + x.TrimStart('|') + "*" + y.TrimStart('|') + "'";
                }
                if (strReturn != ",")
                    strReturn = "{" + strReturn.TrimStart(',') + "}";
                return strReturn;
            }
            catch (Exception ex)
            {
                m_Log.Error("GetAQIChart", ex);
                return ex.ToString();
            }
        }

        private double GetPeriod(DateTime dt)
        {
            double period = 6;
            if (dt.Hour == 6)
                period = 6;
            else if (dt.Hour == 12)
                period = 8;
            else if (dt.Hour == 20)
                period = 10;

            return period;
        }

        private string GetLineStr(DataTable dt, string index)
        {
            string x = "";
            string y = "";
            string z = "";
            DateTime minTime = DateTime.Now;
            DateTime nextTime;
            double lastx = 0;
            double dperiod = 6;
            int times = 0;
            string strReturn = "";
            if (dt.Rows.Count >= 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    if (times == 0)
                    {
                        lastx = double.Parse(dr[0].ToString());
                        x = x + "|" + lastx.ToString();
                        y = y + "|" + (dr[1].ToString() == "" ? " " : dr[1].ToString());
                        z = z + "|" + dr[2].ToString();
                        minTime = DateTime.Parse(dr[3].ToString());
                        dperiod = GetPeriod(minTime);
                    }
                    else
                    {
                        nextTime = DateTime.Parse(dr[3].ToString());
                        while (minTime.AddHours(dperiod) < nextTime)
                        {
                            lastx = lastx + dperiod * 3600;
                            x = x + "|" + lastx.ToString();
                            y = y + "|" + " ";
                            z = z + "|" + " ";
                            minTime = minTime.AddHours(dperiod);
                            dperiod = GetPeriod(minTime);
                        }
                        lastx = double.Parse(dr[0].ToString());
                        x = x + "|" + lastx.ToString();
                        y = y + "|" + (dr[1].ToString() == "" ? " " : dr[1].ToString());
                        z = z + "|" + dr[2].ToString();
                        minTime = nextTime;
                        dperiod = GetPeriod(minTime);
                    }
                    times++;
                }
                strReturn = ",'" + index + "':'" + x.TrimStart('|') + "*" + y.TrimStart('|') + "*" + z.TrimStart('|') + "'";
            }
            return strReturn;
        }

        /// <summary>
        /// 返回AQI对比数据
        /// </summary>
        /// <param name="fromDate"></param>
        /// <param name="toDate"></param>
        /// <param name="typeID"></param>
        /// <returns></returns>
        public string GetAQICompare(string fromDate, string toDate, string typeID, string period, string itemID)
        {
            string strSQL = "";
            string strReturn = "";
            DataTable dt;
            string from = DateTime.Parse(fromDate).ToString("yyyy-MM-dd HH:mm:ss");
            string to = DateTime.Parse(toDate).AddDays(1).ToString("yyyy-MM-dd HH:mm:ss");

            strSQL = "SELECT  MAX(ForecastDate) AS ForecastDate FROM T_ForecastGroup WHERE LST BETWEEN '" + from + "' AND '" + to + "'";
            dt = m_Database.GetDataTable(strSQL);
            string maxForecast = dt.Rows[0][0].ToString();
            string strWhere = " AND LST BETWEEN '" + from + "' AND '" + to + "'";
            string dateFiled = "  DATEDIFF(S,'1970-01-01 00:00:00', LST) AS LST ";
            string period1 = "";
            if (period == "24")
                period1 = "48";
            else
                period1 = "72";
            Dictionary<string, string> dpoint = new Dictionary<string, string>();
            string queryField = ",VALUE,PARAMETER,LST AS BJTIME";
            //是AQI的时候查询的内容变成
            if (itemID == "0")
                queryField = ",AQI,PARAMETER,LST AS BJTIME";

            try
            {
                string strStr = "  UNION SELECT " + dateFiled + queryField + " FROM T_ForecastGroup WHERE ITEMID=" + itemID + " AND durationID IN(2,3,6) ";
                //实测（T_ObsDataGroup）
                if (typeID.IndexOf("0") >= 0)
                {
                    strSQL = "SELECT " + dateFiled + queryField + "  FROM  T_ObsDataGroup WHERE ITEMID=" + itemID + " AND durationID IN(2,3,6) " + strWhere + " ORDER BY LST ASC";
                    dt = m_Database.GetDataTable(strSQL);
                    strReturn = strReturn + GetLineStr(dt, "0");
                }

                if (typeID.IndexOf("1") >= 0)
                {
                    //终合and  not VALUE is null
                    strSQL = "SELECT " + dateFiled + queryField + " FROM T_ForecastGroup WHERE ITEMID=" + itemID + " AND durationID IN(2,3,6) AND PERIOD="
                        + period + " AND module = 'ManualCenter' " + strWhere + strStr + strWhere + " AND module = 'ManualCenter'  AND PERIOD=" + period1 + "  AND ForecastDate= '" + DateTime.Parse(maxForecast).ToString("yyyy/M/d 18:00") + "'  ORDER BY LST ASC";
                    dt = m_Database.GetDataTable(strSQL);
                    strReturn = strReturn + GetLineStr(dt, "1");
                }

                if (typeID.IndexOf("2") >= 0)
                {
                    //WRF
                    strSQL = "SELECT  MAX(ForecastDate) AS ForecastDate FROM T_ForecastGroup WHERE LST BETWEEN '" + from + "' AND '" + to + "' and module = 'WRF'";
                    string max = m_Database.GetFirstValue(strSQL);
                    strSQL = "SELECT " + dateFiled + queryField + " FROM T_ForecastGroup WHERE ITEMID=" + itemID + " AND durationID IN(2,3,6) AND PERIOD="
                        + period + " AND module ='WRF' " + strWhere + strStr + strWhere + " AND module = 'WRF'  AND PERIOD=" + period1 + "   AND ForecastDate= '" + DateTime.Parse(max).ToString("yyyy/M/d 0:00") + "'  ORDER BY LST ASC";
                    dt = m_Database.GetDataTable(strSQL);
                    strReturn = strReturn + GetLineStr(dt, "2");
                }

                if (typeID.IndexOf("3") > 0)
                {
                    //数值CMAQ
                    strSQL = "SELECT  MAX(ForecastDate) AS ForecastDate FROM T_ForecastGroup WHERE LST BETWEEN '" + from + "' AND '" + to + "' and module = 'CMAQ'";
                    string max = m_Database.GetFirstValue(strSQL);
                    strSQL = "SELECT " + dateFiled + queryField + " FROM T_ForecastGroup WHERE ITEMID=" + itemID + " AND durationID IN(2,3,6) AND PERIOD="
                        + period + " AND module ='CMAQ' " + strWhere + strStr + strWhere + " AND module = 'CMAQ'  AND PERIOD=" + period1 + "   AND ForecastDate= '" + DateTime.Parse(max).ToString("yyyy/M/d 20:00") + "' ORDER BY LST ASC";
                    dt = m_Database.GetDataTable(strSQL);
                    strReturn = strReturn + GetLineStr(dt, "3");
                }
                if (typeID.IndexOf("4") > 0)
                {
                    //环境监测

                    strSQL = "SELECT " + dateFiled + queryField + " FROM T_ForecastGroup WHERE ITEMID=" + itemID + " AND durationID IN(2,3,6) AND PERIOD="
                        + period + " AND module ='ManualSubmit' " + strWhere + strStr + strWhere + " AND module = 'ManualSubmit'  AND PERIOD=" + period1 + "   AND ForecastDate= '" + DateTime.Parse(maxForecast).ToString("yyyy/M/d 18:00") + "' ORDER BY LST ASC";
                    dt = m_Database.GetDataTable(strSQL);
                    strReturn = strReturn + GetLineStr(dt, "4");
                }
                if (typeID.IndexOf("5") > 0)
                {
                    //气象局
                    strSQL = "SELECT " + dateFiled + queryField + " FROM T_ForecastGroup WHERE ITEMID=" + itemID + " AND durationID IN(2,3,6) AND PERIOD="
                        + period + " AND module ='SMCSubmit' " + strWhere + strStr + strWhere + " AND module = 'SMCSubmit'  AND PERIOD=" + period1 + "   AND ForecastDate= '" + DateTime.Parse(maxForecast).ToString("yyyy/M/d 18:00") + "' ORDER BY LST ASC";
                    dt = m_Database.GetDataTable(strSQL);
                    strReturn = strReturn + GetLineStr(dt, "5");
                }
                if (strReturn != ",")
                    strReturn = "{" + strReturn.TrimStart(',') + "}";
                return strReturn;
            }
            catch (Exception ex)
            {
                m_Log.Error("GetAQICompare", ex);
                return ex.ToString();
            }
        }
        public string GetMouseOver(string forecastDate, string period, string id)
        {
            string duration = id.Substring(3, 1);
            string itemID = id.Substring(4, 1);
            int timeSpan = int.Parse(id.Substring(1, 1)) - 3;
            string Module = "('CMAQ','WRF','Manual','SMC','ManualCenter','ManualSubmit','SMCSubmit')";
            forecastDate = DateTime.Parse(forecastDate).ToString("yyyy-MM-dd 18:00:00");
            DateTime from = DateTime.Parse(forecastDate).AddDays(timeSpan);
            string strSQL = String.Format("SELECT T_User.Alias,VALUE FROM T_FORECASTGROUP LEFT JOIN T_User on T_User.UserName=T_FORECASTGROUP.MODULE WHERE FORECASTDATE = '{0}' AND MODULE not in {1}  AND durationID='{2}' AND ITEMID='{3}' AND LST between '{4}' and '{5}' ", forecastDate, Module, duration, itemID, from.ToString("yyyy-MM-dd 00:00:00"), from.ToString("yyyy-MM-dd 23:59:59"));
            DataTable dt = m_Database.GetDataTable(strSQL);
            StringBuilder sb = new StringBuilder();
            StringBuilder sm = new StringBuilder();
            sb.Append("<table id='DataTablePrepare'  width='100%' border='0' cellpadding='0' cellspacing='0'>");
            foreach (DataRow rows in dt.Rows)
            {
                if (rows[1].ToString() == "")
                    continue;
                sm.AppendLine("<tr onmousemove='trBgcolor(this)' onmouseout='trNocolor(this)' onmousedown=\"pickTr('" + rows[1].ToString() + "','" + id + "')\">");
                sm.AppendFormat("<td class='tablePrepare'>{0}</td>", rows[0]);
                sm.AppendFormat("<td class='tablePrepare'>{0}</td>", ConvertToAQI(rows[1].ToString(), itemID));
                sm.AppendLine("</tr>");
            }
            if (sm.ToString() == "")
                return "";
            else
            {
                sb.Append(sm.ToString());
                sb.AppendLine("</table>");
                return sb.ToString();
            }
        }


        /// <summary>
        /// 返回PM2.5和臭氧的数据
        /// </summary>
        /// <param name="forecastDate">预报时效</param>
        /// <returns></returns>
        public string GetForecastCompare(string forecastDate, string forecastToDate)
        {
            string strSQL = "";
            string strReturn = "";
            string x = "";
            string y = "";
            string z = "";
            DateTime maxPeriod;
            string forecastMax;
            string strWhere = "";
            DataTable dt;

            strSQL = "SELECT LST,ForecastDate  FROM  T_ForecastSite WHERE (ITEMID = 1 OR  ITEMID = 4) AND durationID =10 AND SiteID =0 AND PERIOD=24 AND LST in (SELECT MAX(LST) AS maxLST FROM  T_ForecastSite WHERE (ITEMID = 1 OR  ITEMID = 4) AND durationID =10 AND SiteID =0 AND PERIOD=24)";
            dt = m_Database.GetDataTable(strSQL);
            maxPeriod = DateTime.Parse(dt.Rows[0][0].ToString());
            forecastMax = DateTime.Parse(dt.Rows[0][1].ToString()).ToString("yyyy-MM-dd HH:mm:ss");

            string forecast = DateTime.Parse(forecastDate).ToString("yyyy-MM-dd HH:mm:ss");
            DateTime forecastTo = DateTime.Parse(forecastToDate);
            string dateFiled = "  DATEDIFF(S,'1970-01-01 00:00:00', LST) AS LST ";
            string queryField = ",VALUE";
            strWhere = " AND LST BETWEEN  '" + forecast + "' AND '" + forecastTo.ToString("yyyy-MM-dd 23:00:00") + "'";
            if (forecastTo > maxPeriod || forecastTo.ToString("yyyy-MM-dd") == maxPeriod.ToString("yyyy-MM-dd"))
            {
                strSQL = "SELECT " + dateFiled + queryField + " FROM T_ForecastSite WHERE ITEMID=1 AND durationID =10 AND SiteID =0 AND PERIOD=24 "
                     + " AND module ='WRF' " + strWhere + " UNION SELECT " + dateFiled + queryField + " FROM T_ForecastSite WHERE ITEMID=1 AND durationID =10 AND SiteID =0 AND module ='WRF' AND ForecastDate='" + forecastMax + "' AND  LST  BETWEEN  '" + maxPeriod + "' AND '" + forecastTo.ToString("yyyy-MM-dd 23:00:00") + "' ORDER BY LST ASC";
            }
            else
            {
                strSQL = "SELECT " + dateFiled + queryField + " FROM T_ForecastSite WHERE ITEMID=1 AND durationID =10 AND SiteID =0 AND PERIOD=24 " + " AND module ='WRF' " + strWhere + " ORDER BY LST ASC";
            }
            //PM2.5

            dt = m_Database.GetDataTable(strSQL);
            x = ""; y = ""; z = "";
            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    x = x + "|" + dr[0].ToString();
                    y = y + "|" + (dr[1].ToString() == "" ? " " : dr[1].ToString());
                    z = z + "|" + "";
                }
                strReturn = strReturn + ",'0':'" + x.TrimStart('|') + "*" + y.TrimStart('|') + "*" + z.TrimStart('|') + "'";
            }

            //臭氧
            if (forecastTo > maxPeriod)
            {
                strSQL = "SELECT " + dateFiled + queryField + " FROM T_ForecastSite WHERE ITEMID=4 AND durationID =10 AND SiteID =0 AND PERIOD=24 "
                     + " AND module ='WRF' " + strWhere + " UNION SELECT " + dateFiled + queryField + " FROM T_ForecastSite WHERE ITEMID=4 AND durationID =10 AND SiteID =0 AND module ='WRF' AND ForecastDate='" + forecastMax + "' AND   LST  BETWEEN  '" + maxPeriod + "' AND '" + forecastTo.ToString("yyyy-MM-dd 23:00:00") + "' ORDER BY LST ASC";
            }
            else
            {
                strSQL = "SELECT " + dateFiled + queryField + " FROM T_ForecastSite WHERE ITEMID=4 AND durationID =10 AND SiteID =0 AND PERIOD=24 " + " AND module ='WRF' " + strWhere + " ORDER BY LST ASC";
            }
            dt = m_Database.GetDataTable(strSQL);
            x = ""; y = ""; z = "";
            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    x = x + "|" + dr[0].ToString();
                    y = y + "|" + (dr[1].ToString() == "" ? " " : dr[1].ToString());
                }
                strReturn = strReturn + ",'1':'" + x.TrimStart('|') + "*" + y.TrimStart('|') + "*" + z.TrimStart('|') + "'";
            }

            if (strReturn != "," && strReturn != "")
                strReturn = "{" + strReturn.TrimStart(',') + "}";
            return strReturn;
        }
        /// 历史数据查询与导出
        public string GetHistoryForecast(string forecastDate, string period)
        {
            //获取起报时间，如果传入的时间为空，那么自动以当前时间为起报时间
            DateTime dtNow = GetManualForecastDate(forecastDate);

            string fromDate = dtNow.ToString("yyyy-MM-dd");
            string toDate = dtNow.AddDays(+m_BackDays).ToString("yyyy-MM-dd 23:59:59");

            //创建综合预报表单实体
            Entity entity = new Entity(m_Database, "Forecast");
            string strSQL = string.Format("FORECASTDATE = '{0}'", dtNow);//起报时间是每天的18点
            strSQL = entity.BuildQuerySQL(strSQL, "Public");
            //创建实况数据SQL
            strSQL = strSQL + ";" + HistoryCreateRealtimeSQL(fromDate, toDate);
            //创建综合预报数据SQL（气象局和环境监测）
            strSQL = strSQL + ";" + String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') - 1 AS LST, DURATIONID, ITEMID ,VALUE,AQI,Parameter FROM T_ForecastGroup WHERE FORECASTDATE = '{0}' AND Module='ManualSubmit';", dtNow.ToString("yyyy-MM-dd HH:00:00"));
            strSQL = strSQL + String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') - 1 AS LST, DURATIONID, ITEMID ,VALUE,AQI,Parameter FROM T_ForecastGroup WHERE FORECASTDATE = '{0}' AND Module='SMCSubmit';", dtNow.ToString("yyyy-MM-dd HH:00:00"));
            strSQL = strSQL + String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') - 1 AS LST, DURATIONID, ITEMID ,VALUE,AQI,Parameter FROM T_ForecastGroup WHERE FORECASTDATE = '{0}' AND Module='ManualCenter';", dtNow.ToString("yyyy-MM-dd HH:00:00"));
            DataSet ds = m_Database.GetDataset(strSQL);
            try
            {
                DataSet dSet = m_Database.GetDataset(strSQL);
                StringBuilder sb = new StringBuilder("{");
                DataTable dTable = dSet.Tables[0];
                string tag;
                string json = GetForecastJSON(dTable);
                sb.Append(json);
                if (json != "")
                    sb.Append(",");

                //生成实况，综合预报，模式数据的json
                for (int i = 0; i < 4; i++)
                {
                    //创建json，便于前台赋值
                    dTable = dSet.Tables[i + 1];
                    if (i == 3)
                        tag = "H";
                    else
                        tag = "P";

                    json = GetGroupJSON(dTable, i, tag);//实况typeID = 0;//综合预报typeID = 1//模式typeID = 2

                    if (json != "")
                    {
                        sb.Append(json);
                        sb.Append(",");
                    }
                }
                sb.Append(string.Format("nowDateTime:'{0}'", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
                if (sb.Length > 1)
                {
                    sb.Append("}");
                }
                else
                    sb.Length = 0;

                return sb.ToString();
            }
            catch (Exception ex)
            {
                m_Log.Error("GetForecast", ex);
                return ex.ToString();
            }
        }
        /// <summary>
        /// </summary>
        /// <param name="hour"></param>
        /// <returns></returns>
        public string GetForecast(string flag, string hour, string period, string Module, string tag, string moduleStyle)
        {
            //获取起报时间，如果传入的时间为空，那么自动以当前时间为起报时间
            DateTime dtNow = GetManualForecastDate(hour);

            string toDate = dtNow.ToString("yyyy-MM-dd 23:59:59");
            string fromDate = dtNow.AddDays(-m_BackDays).ToString("yyyy-MM-dd");

            //创建综合预报表单实体
            Entity entity = new Entity(m_Database, "Forecast");
            string strSQL = string.Format("FORECASTDATE = '{0}'", dtNow);//起报时间是每天的18点
            string Flag = "";
            if (Module == "Manual" || Module == "SMC" || Module == "SMCModify" || Module == "Modify")
                Flag = "Former";
            else if (Module == "ManualCenter" || Module == "SMCCenter")
                Flag = "ManualCenter";
            else
                Flag = Module;
            strSQL = entity.BuildQuerySQL(strSQL, Flag);

            //创建实况数据SQL
            if (flag == "2")//预报回顾标志
                strSQL = strSQL + ";" + CreateRealtimeReSeeSQL(fromDate, toDate);
            else
                strSQL = strSQL + ";" + CreateRealtimeSQL(fromDate, toDate);

            //创建参考综合预报SQL
            if (tag == "zhonghe")
            {
                if (flag == "2")
                    strSQL = strSQL + ";" + CreateComReSeeSQL(fromDate, toDate, period, Module);
                else
                    strSQL = strSQL + ";" + CreateComSQL(fromDate, toDate, period, Module);
            }
            else
                strSQL = strSQL + ";" + CreateComReviseSQL(fromDate, toDate, period, Module);

            //创建历史综合预报
            strSQL = strSQL + " UNION ALL " + CreatComforecastSQL(flag, dtNow.ToString("yyyy-MM-dd HH:00:00"), Module);


            //创建模式预报SQL，数值模式的起报时间是综合预报起报时间的前一天的北京时间20点
            strSQL = strSQL + ";" + CreateModuleSQL(dtNow, moduleStyle);
            try
            {
                DataSet dSet = m_Database.GetDataset(strSQL);
                StringBuilder sb = new StringBuilder("{");
                strSQL = string.Format("FORECASTDATE = '{0}'", dtNow);//起报时间是每天的18点
                strSQL = entity.BuildQuerySQL(strSQL, "Public");
                DataTable formJson = m_Database.GetDataTable(strSQL);
                //创建表单json
                DataTable dTable = dSet.Tables[0];
                if (formJson.Rows.Count > 0)
                {
                    if (dTable.Rows.Count > 0)
                    {
                        dTable.Rows[0]["H056"] = formJson.Rows[0]["H056"];
                        dTable.Rows[0]["H052"] = formJson.Rows[0]["H052"];
                        dTable.Rows[0]["H053"] = formJson.Rows[0]["H053"];
                        dTable.Rows[0]["H066"] = formJson.Rows[0]["H066"];
                        dTable.Rows[0]["H062"] = formJson.Rows[0]["H062"];
                        dTable.Rows[0]["H063"] = formJson.Rows[0]["H063"];
                    }
                }
                string json = GetForecastJSON(dTable);
                sb.Append(json);
                if (json != "")
                    sb.Append(",");

                //生成实况，综合预报，模式数据的json
                for (int i = 0; i < 3; i++)
                {
                    //创建json，便于前台赋值
                    dTable = dSet.Tables[i + 1];
                    json = GetGroupJSON(dTable, i, "H");//实况typeID = 0;//综合预报typeID = 1//模式typeID = 2

                    if (json != "")
                    {
                        sb.Append(json);
                        sb.Append(",");
                    }
                }
                sb.Append(string.Format("nowDateTime:'{0}'", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
                if (sb.Length > 1)
                {
                    sb.Append("}");
                }
                else
                    sb.Length = 0;

                return sb.ToString();
            }
            catch (Exception ex)
            {
                m_Log.Error("GetForecast", ex);
                return ex.ToString();
            }

        }
        public DataTable StrSQLString(string fromDate, string toDate, string period, string[] durationID, string dataType, string[] modualArray, string[] userArray)
        {
            DateTime startDate = DateTime.Parse(fromDate);
            DateTime endDate = DateTime.Parse(toDate).AddDays(1);
            string strSQL, durationIDSQL;
            string timeSQL = "LST BETWEEN  '" + startDate + "' AND '" + endDate + "' AND PERIOD =" + period + "";
            durationIDSQL = "LST BETWEEN  '" + startDate + "' AND '" + endDate + "'";
            if (durationID.Length > 0)
            {
                strSQL = "(";
                for (int i = 0; i < durationID.Length; i++)
                {
                    strSQL = strSQL + durationID[i] + ",";
                }
                strSQL = strSQL.Substring(0, strSQL.Length - 1) + ")";
                durationIDSQL = durationIDSQL + " AND durationID IN" + strSQL;
                timeSQL = timeSQL + " AND durationID IN" + strSQL;
            }

            if (modualArray.Length > 0)
            {
                strSQL = "(";
                for (int i = 0; i < modualArray.Length; i++)
                {
                    if (modualArray[i] != "Checkbox")
                    {
                        strSQL = strSQL + "'" + modualArray[i] + "',";
                    }
                    else
                    {
                        if (userArray.Length > 0)
                        {
                            for (int m = 0; m < userArray.Length; m++)
                            {
                                strSQL = strSQL + "'" + userArray[m] + "',";
                            }
                        }
                    }
                }
                strSQL = strSQL.Substring(0, strSQL.Length - 1) + ")";
                timeSQL = timeSQL + " AND Module  IN" + strSQL;

            }
            else
            {
                timeSQL = timeSQL + " AND Module  =''";
            }
            strSQL = "SELECT LST,durationID,ITEMID,Module,Value, AQI,Parameter FROM T_ForecastGroup WHERE " + timeSQL;

            DataTable dtForecastGroup = m_Database.GetDataTable(strSQL);
            DataTable dtShiceGroup = new DataTable();
            if (dataType != "")
            {
                strSQL = "SELECT LST,durationID, ITEMID,'shiCe' as Module, Value, AQI,Parameter FROM T_ObsDataGroup Where " + durationIDSQL + "ORDER BY ITEMID";
                dtShiceGroup = m_Database.GetDataTable(strSQL);
                dtForecastGroup.Merge(dtShiceGroup, false);
            }
            return dtForecastGroup;
        }
        //根据选择条件创建数据表格
        public string GetFilterDataTables(string fromDate, string toDate, string period, string forecasPeriod, string dataType, string dataModule, string users)
        {
            string[] durationID = forecasPeriod.Split(',');
            string[] userArray = users.Split(',');
            string[] modualArray = dataModule.Split(',');
            DataTable ds = StrSQLString(fromDate, toDate, period, durationID, dataType, modualArray, userArray);
            string jsonString = tableString(ds, fromDate, toDate, period, durationID, dataType, modualArray, userArray);
            return jsonString;
        }

        public DataTable tableAQI(DataTable dt, DataTable dm, DateTime startDate, DateTime endTime, string duration, string period)
        {
            DataTable dSearch = new DataTable("T_ForecastAQI");
            dSearch.Columns.Add("LST", typeof(string));
            dSearch.Columns.Add("sectionStr", typeof(string));
            dSearch.Columns.Add("timeSpan", typeof(string));
            dSearch.Columns.Add("PERIOD", typeof(string));
            dSearch.Columns.Add("shiceAQI", typeof(int));
            dSearch.Columns.Add("shiceParameter", typeof(string));
            dSearch.Columns.Add("comAQI", typeof(int));
            dSearch.Columns.Add("comParameter", typeof(string));
            dSearch.Columns.Add("CMAQAQI", typeof(int));
            dSearch.Columns.Add("CMAQParameter", typeof(string));
            dSearch.Columns.Add("WRFAQI", typeof(int));
            dSearch.Columns.Add("WRFParameter", typeof(string));
            string strFilter = "";
            string strSQL = "SELECT DM,MC,Description FROM D_DurationTest";
            DataTable timePeriod = m_Database.GetDataTable(strSQL);
            DataTable temp = dSearch.Clone();
            temp.TableName = string.Format("table{0}", 0);
            string[] durationItems = duration.Split(',');
            for (int j = 0; j < Math.Abs(endTime.Day - startDate.Day); j++)
            {
                if (duration != "")
                {
                    for (int k = 0; k < durationItems.Length; k++)
                    {
                        strFilter = string.Format("ITEMID={0} AND LST>='{1}' AND LST<'{2}' AND durationID={3}", 0, startDate.AddDays(j).ToString(), startDate.AddDays(j + 1).ToString(), int.Parse(durationItems[k].ToString()));
                        DataRow[] rows = dt.Select(strFilter);

                        if (rows.Length > 0)
                        {
                            DataRow dr = rows[0];
                            int duraID = int.Parse(durationItems[k].ToString());
                            DataRow newRow = temp.NewRow();
                            newRow[0] = DateTime.Parse(dr[0].ToString()).ToLongDateString();
                            newRow[3] = dr[2].ToString() + "小时";
                            string[] duratonSpan = GetDurationSpan(timePeriod, duraID);
                            string span = string.Format("{0}:00", int.Parse(duratonSpan[0])) + "-" + string.Format("{0}:00", int.Parse(duratonSpan[1]));
                            newRow[2] = span;
                            string Filter = string.Format("DM={0}", duraID);
                            DataRow[] rowDuation = timePeriod.Select(Filter);
                            newRow[1] = rowDuation[0][2].ToString();
                            foreach (DataRow dataR in rows)
                            {
                                string modualType = dataR[3].ToString();
                                switch (modualType)
                                {
                                    case "CMAQ":
                                        if (dataR[7].ToString() == "")
                                            newRow[8] = DBNull.Value;
                                        else
                                            newRow[8] = int.Parse(dataR[7].ToString());
                                        if (dataR[8].ToString() == "")
                                            newRow[9] = DBNull.Value;
                                        else
                                            newRow[9] = dataR[8].ToString();
                                        break;
                                    case "Manual":
                                        if (dataR[7].ToString() == "")
                                            newRow[6] = DBNull.Value;
                                        else
                                            newRow[6] = int.Parse(dataR[7].ToString());
                                        if (dataR[8].ToString() == "")
                                            newRow[7] = DBNull.Value;
                                        else
                                            newRow[7] = dataR[8].ToString();
                                        break;
                                    case "WRF":
                                        if (dataR[7].ToString() == "")
                                            newRow[10] = DBNull.Value;
                                        else
                                            newRow[10] = int.Parse(dataR[7].ToString());
                                        if (dataR[8].ToString() == "")
                                            newRow[11] = DBNull.Value;
                                        else
                                            newRow[11] = dataR[8].ToString();
                                        break;
                                }
                            }
                            if (dm.Rows.Count > 0)
                            {
                                DataRow[] shiceRow = dm.Select(strFilter);
                                if (shiceRow.Length > 0)
                                {
                                    string para = shiceRow[0][5].ToString();
                                    newRow[5] = para;
                                    int n4 = 0;
                                    int.TryParse(shiceRow[0][4].ToString(), out n4);
                                    newRow[4] = n4;
                                }

                            }
                            else
                            {
                                newRow[4] = DBNull.Value;
                                newRow[5] = DBNull.Value;
                            }
                            temp.Rows.Add(newRow);
                        }
                        else
                        {
                            if (dm.Rows.Count > 0)
                            {
                                DataRow[] shiceRow = dm.Select(strFilter);
                                if (shiceRow.Length > 0)
                                {
                                    DataRow dr = shiceRow[0];
                                    int duraID = int.Parse(durationItems[k].ToString());
                                    DataRow newRow = temp.NewRow();
                                    newRow[0] = DateTime.Parse(dr[0].ToString()).ToLongDateString();
                                    newRow[3] = period + "小时";
                                    string[] duratonSpan = GetDurationSpan(timePeriod, duraID);
                                    string span = string.Format("{0}:00", int.Parse(duratonSpan[0])) + "-" + string.Format("{0}:00", int.Parse(duratonSpan[1]));
                                    newRow[2] = span;
                                    string Filter = string.Format("DM={0}", duraID);
                                    DataRow[] rowDuation = timePeriod.Select(Filter);
                                    newRow[1] = rowDuation[0][2].ToString();

                                    string value = dr[5].ToString();
                                    newRow[5] = value;
                                    newRow[4] = int.Parse(dr[4].ToString());
                                    temp.Rows.Add(newRow);
                                }

                            }

                        }

                    }
                }
            }

            return temp;

        }
        public string tableString(DataTable temp, string fromDate, string toDate, string period, string[] forecasPeriod, string dataType, string[] dataModule, string[] users)
        {
            StringBuilder sb = new StringBuilder();
            AQIExtention aqiExt;
            string style = "";
            string filter = "";
            if (dataType != "")
                style = style + "shiCe,";
            if (dataModule.Length > 0)
            {
                for (int i = 0; i < dataModule.Length; i++)
                {
                    if (dataModule[i] != "")
                    {
                        if (dataModule[i] != "Checkbox")
                        {
                            style = style + dataModule[i] + ",";
                        }
                        else
                        {
                            if (users.Length > 0)
                            {
                                for (int k = 0; k < users.Length; k++)
                                {
                                    style = style + users[k] + ",";
                                }
                            }
                        }
                    }
                }
            }
            string forecastCount = "";
            for (int m = 0; m < forecasPeriod.Length; m++)
            {
                forecastCount = forecastCount + forecasPeriod[m] + ",";
            }
            forecastCount = "(" + forecastCount.Substring(0, forecastCount.Length - 1) + ")";
            string[] stylesArray = style.Substring(0, style.Length - 1).Split(',');
            string[] nameArray = { "PM2.5", "PM10", "NO2", "03-1h", "03-8h" };
            int length = stylesArray.Length;
            string strSQL = "SELECT DM,MC,Description FROM D_DurationTest";
            DataTable timePeriod = m_Database.GetDataTable(strSQL);
            DateTime startDate = DateTime.Parse(fromDate);
            DateTime endDate = DateTime.Parse(toDate);
            TimeSpan timespan = new TimeSpan();
            timespan = endDate - startDate;
            int day = timespan.Days;
            int daySpan = 0;
            string aqiColor = "";
            int count = 4 + stylesArray.Length * 2;
            double width = 100 / count;
            bool flag = false;
            for (int i = 0; i < 6; i++)
            {
                sb.AppendFormat("<table id='table{0}'  width='100%' border='0' cellpadding='0' cellspacing='0' class='tablekuang'>", i);
                sb.AppendLine("<tr>");
                sb.AppendFormat("<td class='tabletitleFilter' style='width:{0}%' rowspan='2'>日期</td>", width);
                sb.AppendFormat("<td class='tabletitleFilter' style='width:{0}%' rowspan='2'>时段名称</td>", width);
                sb.AppendFormat("<td class='tabletitleFilter' style='width:{0}%' rowspan='2'>时段区间</td>", width);
                sb.AppendFormat("<td class='tabletitleFilter' style='width:{0}%' rowspan='2'>预报时效</td>", width);
                if (i == 0)
                {
                    for (int j = 0; j < stylesArray.Length; j++)
                    {
                        sb.AppendFormat("<td class='tabletitleFilter' style='width:{1}%' colspan='2'>{0}</td>", retureStyleName(stylesArray[j]), width);
                    }
                    sb.AppendLine("</tr>");
                    sb.AppendLine("<tr>");
                    for (int j = 0; j < stylesArray.Length; j++)
                    {
                        sb.AppendFormat("<td class='tabletitleFilter' style='width:{0}%'>AQI</td>", width);
                        sb.AppendFormat("<td class='tabletitleFilter' style='width:{0}%'>首要污染物</td>", width);
                    }
                    sb.AppendLine("</tr>");
                }
                else
                {
                    sb.AppendFormat("<td class='tabletitleFilter' style='width:{2}%' colspan='{0}'>{1}浓度</td>", length, nameArray[i - 1], width);
                    sb.AppendFormat("<td class='tabletitleFilter' style='width:{2}%' colspan='{0}'>{1}AQI</td>", length, nameArray[i - 1], width);
                    sb.AppendLine("</tr>");
                    sb.AppendLine("<tr>");
                    for (int t = 0; t < 2; t++)
                    {
                        for (int j = 0; j < stylesArray.Length; j++)
                        {
                            sb.AppendFormat("<td class='tabletitleFilter' style='width:{1}%'>{0}</td>", retureStyleName(stylesArray[j]), width);
                        }
                    }
                    sb.AppendLine("</tr>");
                }

                int k = 0;
                for (int j = 0; j < day; j++)
                {
                    daySpan = 0;
                    flag = true;
                    if (forecasPeriod.Length > 0)
                    {
                        for (int t = 0; t < forecasPeriod.Length; t++)
                        {
                            filter = string.Format("ITEMID={0} AND LST>='{1}' AND LST<'{2}' AND durationID={3}", i, startDate.AddDays(j).ToString(), startDate.AddDays(j + 1).ToString(), int.Parse(forecasPeriod[t]));
                            DataRow[] rows = temp.Select(filter);
                            if (rows.Length > 0)
                                daySpan++;
                        }
                        for (int t = 0; t < forecasPeriod.Length; t++)
                        {
                            k++;
                            filter = string.Format("ITEMID={0} AND LST>='{1}' AND LST<'{2}' AND durationID={3}", i, startDate.AddDays(j).ToString(), startDate.AddDays(j + 1).ToString(), int.Parse(forecasPeriod[t]));
                            DataRow[] rows = temp.Select(filter);
                            if (rows.Length > 0)
                            {
                                sb.AppendLine(string.Format("<tr  onmouseover='mouseOver(this)' onmouseout='mouseOut(this)' id='{0}'>", "table" + i.ToString() + k.ToString()));
                                DataRow dr = rows[0];
                                if (flag)
                                {
                                    flag = false;
                                    sb.AppendFormat("<td class='tablerowFilter' style='width:{2}%' rowspan='{1}' id='table{3}0'>{0}</td>", DateTime.Parse(dr[0].ToString()).ToString("yyyy年MM月dd日"), daySpan, width, i, k);
                                }
                                int duraID = int.Parse(forecasPeriod[t].ToString());
                                string Filter = string.Format("DM={0}", duraID);
                                DataRow[] rowDuation = timePeriod.Select(Filter);
                                sb.AppendFormat("<td style='width:{1}%' class='tablerowFilter'>{0}</td>", rowDuation[0][2].ToString(), width);

                                string[] duratonSpan = GetDurationSpan(timePeriod, duraID);
                                string span = string.Format("{0}:00", int.Parse(duratonSpan[0])) + "-" + string.Format("{0}:00", int.Parse(duratonSpan[1]));
                                sb.AppendFormat("<td class='tablerowFilter' style='width:{1}%'>{0}</td>", span, width);

                                sb.AppendFormat("<td class='tablerowFilter' style='width:{1}%'>{0}</td>", period + "小时", width);
                                if (i == 0)
                                {
                                    for (int s = 0; s < stylesArray.Length; s++)
                                    {
                                        filter = string.Format("ITEMID={0} AND LST>='{1}' AND LST<'{2}' AND durationID={3} AND Module='{4}'", i, startDate.AddDays(j).ToString(), startDate.AddDays(j + 1).ToString(), int.Parse(forecasPeriod[t]), stylesArray[s]);
                                        DataRow[] rowchild = temp.Select(filter);

                                        if (rowchild.Length > 0)
                                        {
                                            aqiExt = new AQIExtention(int.Parse(rowchild[0][5].ToString()), retureItemsID(rowchild[0][6].ToString()));
                                            aqiColor = string.Format("class='{0}'", aqiExt.Color);
                                            sb.AppendFormat("<td class='tablerowFilter'  style='width:{2}%'><span {0}>{1}</span></td>", aqiColor, rowchild[0][5], width);
                                            sb.AppendFormat("<td class='tablerowFilter'  style='width:{1}%'>{0}</td>", rowchild[0][6], width);
                                        }
                                        else
                                        {
                                            sb.AppendFormat("<td class='tablerowFilter'   style='width:{0}%'>/</td>", width);
                                            sb.AppendFormat("<td class='tablerowFilter'   style='width:{0}%'>/</td>", width);
                                        }
                                    }
                                }
                                else
                                {
                                    for (int n = 0; n < 2; n++)
                                    {
                                        for (int s = 0; s < stylesArray.Length; s++)
                                        {
                                            filter = string.Format("ITEMID={0} AND LST>='{1}' AND LST<'{2}' AND durationID={3} AND Module='{4}'", i, startDate.AddDays(j).ToString(), startDate.AddDays(j + 1).ToString(), int.Parse(forecasPeriod[t]), stylesArray[s]);
                                            DataRow[] rowchild = temp.Select(filter);
                                            if (rowchild.Length > 0)
                                            {
                                                if (n == 1)
                                                {
                                                    if (rowchild[0][5].ToString() != "")
                                                    {
                                                        aqiExt = new AQIExtention(int.Parse(rowchild[0][5].ToString()), i);
                                                        aqiColor = string.Format("class='{0}'", aqiExt.Color);
                                                        sb.AppendFormat("<td class='tablerowFilter'  style='width:{2}%'><span {0}>{1}</span></td>", aqiColor, rowchild[0][5], width);
                                                    }
                                                    else
                                                        sb.AppendFormat("<td class='tablerowFilter'  style='width:{0}%'>/</td>", width);

                                                }
                                                else
                                                    sb.AppendFormat("<td class='tablerowFilter'  style='width:{1}%'>{0}</td>", rowchild[0][4], width);
                                            }
                                            else
                                                sb.AppendFormat("<td class='tablerowFilter'  style='width:{0}%'>/</td>", width);
                                        }
                                    }
                                }
                            }

                        }
                    }
                }
                sb.AppendLine("</table>|");
            }

            string json = sb.ToString();
            int posi = json.LastIndexOf("|");
            string returnJson = json.Substring(0, posi);
            return returnJson;
        }
        public int retureItemsID(string itemName)
        {
            int ItemsID = 1;
            switch (itemName)
            {
                case "PM2.5":
                    ItemsID = 1;
                    break;
                case "PM10":
                    ItemsID = 2;
                    break;
                case "NO2":
                    ItemsID = 3;
                    break;
                case "03-1h":
                    ItemsID = 4;
                    break;
                case "03-8h":
                    ItemsID = 5;
                    break;
            }
            return ItemsID;

        }
        public string retureStyleName(string style)
        {
            string name = "";
            switch (style)
            {
                case "shiCe":
                    name = "实测";
                    break;
                case "ManualCenter":
                    name = "外部会商";
                    break;
                case "ManualSubmit":
                    name = "环境监测";
                    break;
                case "SMCSubmit":
                    name = "气象局";
                    break;
                default:
                    name = style;
                    break;
            }
            return name;

        }

        public DataSet tableUnion(DataTable dt, DataTable dm, DateTime startDate, DateTime endTime, string durationID, string period)
        {

            DataTable dSearch = new DataTable("T_ForecastSearch");
            dSearch.Columns.Add("LST", typeof(string));
            dSearch.Columns.Add("sectionStr", typeof(string));
            dSearch.Columns.Add("timeSpan", typeof(string));
            dSearch.Columns.Add("PERIOD", typeof(string));
            dSearch.Columns.Add("shiCeValue", typeof(double));
            dSearch.Columns.Add("comValue", typeof(double));
            dSearch.Columns.Add("CMAQValue", typeof(double));
            dSearch.Columns.Add("WRFValue", typeof(double));

            dSearch.Columns.Add("shiCeAQI", typeof(int));
            dSearch.Columns.Add("comAQI", typeof(int));
            dSearch.Columns.Add("CMAQAQI", typeof(int));
            dSearch.Columns.Add("WRFAQI", typeof(int));
            DataSet tmpTable = new DataSet();
            string strFilter = "";

            string strSQL = "SELECT DM,MC,Description FROM D_DurationTest";
            DataTable timePeriod = m_Database.GetDataTable(strSQL);
            for (int i = 1; i <= 5; i++)
            {

                DataTable temp = dSearch.Clone();
                temp.TableName = string.Format("table{0}", i);
                string[] durationItems = durationID.Split(',');
                TimeSpan ts = endTime - startDate;
                int s = ts.Days;
                for (int j = 0; j < Math.Abs(s); j++)
                {
                    if (durationID != "")
                    {
                        for (int k = 0; k < durationItems.Length; k++)
                        {
                            strFilter = string.Format("ITEMID={0} AND LST>='{1}' AND LST<'{2}' AND durationID={3}", i, startDate.AddDays(j).ToString(), startDate.AddDays(j + 1).ToString(), int.Parse(durationItems[k].ToString()));
                            DataRow[] rows = dt.Select(strFilter);

                            if (rows.Length > 0)
                            {
                                DataRow dr = rows[0];
                                int duraID = int.Parse(durationItems[k].ToString());
                                DataRow newRow = temp.NewRow();
                                newRow[0] = DateTime.Parse(dr[0].ToString()).ToLongDateString();
                                newRow[3] = dr[2].ToString() + "小时";
                                string[] duratonSpan = GetDurationSpan(timePeriod, duraID);
                                string span = string.Format("{0}:00", int.Parse(duratonSpan[0])) + "-" + string.Format("{0}:00", int.Parse(duratonSpan[1]));
                                newRow[2] = span;
                                string Filter = string.Format("DM={0}", duraID);
                                DataRow[] rowDuation = timePeriod.Select(Filter);
                                newRow[1] = rowDuation[0][2].ToString();
                                foreach (DataRow dataR in rows)
                                {
                                    string modualType = dataR[3].ToString();
                                    switch (modualType)
                                    {
                                        case "CMAQ":
                                            if (dataR[6].ToString() == "")
                                            {
                                                newRow[6] = DBNull.Value;
                                            }
                                            else
                                            {
                                                newRow[6] = Math.Round(double.Parse(dataR[6].ToString()), 1);
                                            }
                                            if (dataR[7].ToString() == "")
                                            {
                                                newRow[10] = DBNull.Value;
                                            }
                                            else
                                            {
                                                newRow[10] = int.Parse(dataR[7].ToString());
                                            }
                                            break;
                                        case "Manual":
                                            if (dataR[6].ToString() == "")
                                            {
                                                newRow[5] = DBNull.Value;

                                            }
                                            else
                                            {
                                                newRow[5] = Math.Round(double.Parse(dataR[6].ToString()), 1);
                                            }
                                            if (dataR[7].ToString() == "")
                                            {
                                                newRow[9] = DBNull.Value;

                                            }
                                            else
                                            {

                                                newRow[9] = int.Parse(dataR[7].ToString());
                                            }
                                            break;
                                        case "WRF":
                                            if (dataR[6].ToString() == "")
                                            {
                                                newRow[7] = DBNull.Value;
                                            }
                                            else
                                            {
                                                newRow[7] = Math.Round(double.Parse(dataR[6].ToString()), 1);
                                            }
                                            if (dataR[7].ToString() == "")
                                            {
                                                newRow[11] = DBNull.Value;
                                            }
                                            else
                                            {
                                                newRow[11] = int.Parse(dataR[7].ToString());
                                            }
                                            break;
                                    }
                                }
                                if (dm.Rows.Count > 0)
                                {
                                    DataRow[] shiceRow = dm.Select(strFilter);
                                    if (shiceRow.Length > 0)
                                    {
                                        double sc3 = 0d;
                                        double.TryParse(shiceRow[0][3].ToString(), out sc3);
                                        double value = sc3;
                                        newRow[4] = Math.Round(value, 1);
                                        int sc4 = 0;
                                        int.TryParse(shiceRow[0][4].ToString(), out sc4);
                                        newRow[8] = sc4;
                                    }

                                }
                                else
                                {
                                    newRow[4] = DBNull.Value;
                                    newRow[8] = DBNull.Value;
                                }
                                temp.Rows.Add(newRow);
                            }
                            else
                            {
                                if (dm.Rows.Count > 0)
                                {
                                    DataRow[] shiceRow = dm.Select(strFilter);
                                    if (shiceRow.Length > 0)
                                    {
                                        DataRow dr = shiceRow[0];
                                        int duraID = int.Parse(durationItems[k].ToString());
                                        DataRow newRow = temp.NewRow();
                                        newRow[0] = DateTime.Parse(dr[0].ToString()).ToLongDateString();
                                        newRow[3] = period + "小时";
                                        string[] duratonSpan = GetDurationSpan(timePeriod, duraID);
                                        string span = string.Format("{0}:00", int.Parse(duratonSpan[0])) + "-" + string.Format("{0}:00", int.Parse(duratonSpan[1]));
                                        newRow[2] = span;
                                        string Filter = string.Format("DM={0}", duraID);
                                        DataRow[] rowDuation = timePeriod.Select(Filter);
                                        newRow[1] = rowDuation[0][2].ToString();

                                        double sc3 = 0d;
                                        double.TryParse(shiceRow[0][3].ToString(), out sc3);
                                        double value = sc3;
                                        newRow[4] = Math.Round(value, 1);
                                        int sc4 = 0;
                                        int.TryParse(shiceRow[0][4].ToString(), out sc4);
                                        newRow[8] = sc4;
                                        temp.Rows.Add(newRow);
                                    }

                                }

                            }
                        }
                    }
                }
                tmpTable.Tables.Add(temp);
            }
            return tmpTable;
        }
        /// <summary>
        /// 根据模式名称获取模式预报的数据
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        public string GetModuleForecast(string hour, string module)
        {
            ////创建模式预报SQL
            //获取起报时间，如果传入的时间为空，那么自动以当前时间为起报时间
            DateTime dtNow = GetManualForecastDate(hour);

            string strSQL = CreateModuleSQL(dtNow, module);

            DataTable dTable = m_Database.GetDataTable(strSQL);
            StringBuilder sb = new StringBuilder("{");
            string json;
            //生成实况，综合预报，模式数据的json
            if (dTable.Rows.Count > 0)
            {
                //创建json，便于前台赋值
                json = GetGroupJSON(dTable, 2, "H");//实况typeID = 0;//综合预报typeID = 1//模式typeID = 2

                if (json != "")
                {
                    sb.Append(json);
                    sb.Append(",");
                }

            }

            if (sb.Length > 1)
            {
                sb.Remove(sb.Length - 1, 1);
                sb.Append("}");
            }
            else
                sb.Length = 0;

            return sb.ToString();

        }
        /// <summary>
        /// 根据日期和时效返回综合预报的数据
        /// </summary>
        /// <param name="hour">此日期包含在前台的顺序信息</param>
        /// <param name="period">24小时，48小时</param>
        /// <returns></returns>
        public string GetForecastByPeriod(string hour, string period, string Module)
        {
            //获取起报时间，如果传入的时间为空，那么自动以当前时间为起报时间
            string[] part = hour.Split(';');
            string strSQL = "";

            StringBuilder sb = new StringBuilder("{");
            for (int i = 0; i < part.Length - 1; i++)
            {
                string date = part[i];
                DateTime dtNow = DateTime.Parse(date);
                string toDate = dtNow.ToString("yyyy-MM-dd 23:59:59");
                string fromDate = dtNow.ToString("yyyy-MM-dd");


                strSQL = strSQL + CreateComSQL(fromDate, toDate, period, Module) + ";";
                if (part.Length == 2)
                    strSQL = strSQL + CreateRealtimeSQL(fromDate, toDate);


            }
            DataSet dSet = m_Database.GetDataset(strSQL);
            int typeID = 1;
            for (int j = 0; j < dSet.Tables.Count; j++)
            {
                if (part.Length == 2)
                {
                    m_BackDays = int.Parse(part[1]);
                    typeID = 1 - j;
                }
                else
                    m_BackDays = j;

                //创建json，便于前台赋值
                DataTable dTable = dSet.Tables[j];
                string json = GetGroupJSON(dTable, typeID, "H");//实况typeID = 0;//综合预报typeID = 1//模式typeID = 2
                if (json != "")
                {
                    sb.Append(json);
                    sb.Append(",");
                }
            }
            //标准化sb
            if (sb.Length > 1)
            {
                sb.Remove(sb.Length - 1, 1);
                sb.Append("}");
            }
            else
                sb.Length = 0;

            return sb.ToString();

        }

        /// <summary>
        /// 根据日期和时效返回综合预报的数据
        /// </summary>
        /// <param name="days">此日期包含在前台的顺序信息</param>
        /// <param name="period">24小时，48小时</param>
        /// <returns></returns>
        public string GetComForecast(string days, string period)
        {

            return "";
        }

        /// <summary>
        /// 根据浓度值和污染物ID，返回浓度和AQI的组合值，并具有颜色标识
        /// </summary>
        /// <param name="value"></param>
        /// <param name="itemID"></param>
        /// <returns></returns>
        public string ConvertToAQICopy(string value, string itemID)
        {

            int AQIValue = ToAQI(value, itemID);
            AQIExtention aqiExt = new AQIExtention(AQIValue, int.Parse(itemID));
            string aqiColor = string.Format("class='{0}'", aqiExt.Color);
            return string.Format("{0}/<span {1}>{2}</span>", value, aqiColor, AQIValue);
        }

        public string ConvertToAQI(string value, string itemID)
        {

            int AQIValue = ToAQI(value, itemID);
            AQIExtention aqiExt = new AQIExtention(AQIValue, int.Parse(itemID));
            string aqiColor = string.Format("class='{0}'", aqiExt.Color);
            return string.Format("{0}/<span {1}>{2}</span>", value, aqiColor, AQIValue);
        }

        public int ToAQICopy(string value, string itemID)
        {
            int AQIValue = 0;
            double inputValue = double.Parse(value) / 1000;
            switch (itemID)
            {
                case "1":
                    AQIValue = Lucas.AQI2012.ConvertAQI.ConvertToAQI(inputValue, 24, 11, 180);
                    break;
                case "2":
                    AQIValue = Lucas.AQI2012.ConvertAQI.ConvertToAQI(inputValue, 7, 11, 180);
                    break;
                case "3":
                    AQIValue = Lucas.AQI2012.ConvertAQI.ConvertToAQI(inputValue, 22, 10, 0);
                    break;
                case "4":
                    AQIValue = Lucas.AQI2012.ConvertAQI.ConvertToAQI(inputValue, 8, 10, 0);
                    break;
                case "5":
                    AQIValue = Lucas.AQI2012.ConvertAQI.ConvertToAQI(inputValue, 8, 16, 16);
                    break;
            }

            return AQIValue;
        }

        //2016年1月21日，修改NO2计算方法
        public int ToAQI(string value, string itemID)
        {
            int AQIValue = 0;
            double inputValue = double.Parse(value) / 1000;
            switch (itemID)
            {
                case "1":
                    AQIValue = Lucas.AQI2012.ConvertAQI.ConvertToAQI(inputValue, 24, 11, 180);
                    break;
                case "2":
                    AQIValue = Lucas.AQI2012.ConvertAQI.ConvertToAQI(inputValue, 7, 11, 180);
                    break;
                case "3":
                    AQIValue = Lucas.AQI2012.ConvertAQI.ConvertToAQI(inputValue, 22, 11, 180);
                    break;
                //case "3":
                //    AQIValue = Lucas.AQI2012.ConvertAQI.ConvertToAQI(inputValue, 22, 10, 0);
                //    break;
                case "4":
                    AQIValue = Lucas.AQI2012.ConvertAQI.ConvertToAQI(inputValue, 8, 10, 0);
                    break;
                case "5":
                    AQIValue = Lucas.AQI2012.ConvertAQI.ConvertToAQI(inputValue, 8, 16, 16);
                    break;
            }
            return AQIValue;
        }

        /// <summary>
        /// 获取预报表单的JSON
        /// </summary>
        /// <param name="forecast"></param>
        /// <returns></returns>
        private string GetForecastJSON(DataTable forecast)
        {
            StringBuilder sb = new StringBuilder();
            if (forecast.Rows.Count > 0)
            {

                for (int i = 0; i < forecast.Columns.Count; i++)
                {
                    sb.Append(string.Format("{0}:'{1}',", forecast.Columns[i].ColumnName, forecast.Rows[0][i]));
                }
                //去掉多余的“,”
                if (sb.Length > 1)
                {
                    sb.Remove(sb.Length - 1, 1);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 根据传入的分段预报表，获取能够被前台识别的JSON
        /// </summary>
        /// <param name="dtGroup">分段与报表</param>
        /// <returns></returns>
        private string GetGroupJSON(DataTable dtGroup, int typeID, string tag)
        {
            StringBuilder sb = new StringBuilder();
            if (dtGroup.Rows.Count > 0)
            {
                AQIExtention aqiExt;
                string aqiColor = "";
                int spanDays;
                DataTable dtGroupFilter = null;
                if (dtGroup.Columns.Count > 5)
                {
                    dtGroupFilter = dtGroup.DefaultView.ToTable(false, "LST", "DURATIONID", "ITEMID", "VALUE", "AQI", "Parameter");
                }
                else
                {
                    dtGroupFilter = dtGroup;
                }
                foreach (DataRow row in dtGroupFilter.Rows)
                {
                    if (dtGroup.Columns[0].DataType == typeof(DateTime))
                    {
                        DateTime forcast = DateTime.Parse(dtGroup.Rows[0][1].ToString());
                        TimeSpan timeSpan = forcast.Date - DateTime.Parse(row[0].ToString()).Date;
                        spanDays = timeSpan.Days - 1;
                    }
                    else
                    {
                        spanDays = int.Parse(row[0].ToString());
                    }
                    if (row[4].ToString() != "" && row[2].ToString() != "")
                    {
                        aqiExt = new AQIExtention(int.Parse(row[4].ToString()), int.Parse(row[2].ToString()));
                        aqiColor = string.Format("class='{0}'", aqiExt.Color);
                    }
                    if (int.Parse(row[2].ToString()) == 0 && dtGroupFilter.Columns.Count > 5)
                        sb.Append(string.Format("{7}{0}{1}{2}{3}:\"<span {5}>{6}</span>/{4}\",", m_BackDays - spanDays, typeID, row[1], 6, row[5], aqiColor, row[4], tag));
                    else
                        sb.Append(string.Format("{7}{0}{1}{2}{3}:\"{4}/<span {5}>{6}</span>\",", m_BackDays - spanDays, typeID, row[1], row[2], row[3], aqiColor, row[4], tag));

                }
                //去掉多余的“,”
                if (sb.Length > 1)
                {
                    sb.Remove(sb.Length - 1, 1);
                }
            }
            return sb.ToString();
        }

        //闫海涛修改，数据库中只有气象局的数据，监测中心自动填充为气象局数据
        private string GetGroupJSONSingle(DataTable dtGroup, int typeID, string tag)
        {
            StringBuilder sb = new StringBuilder();
            if (dtGroup.Rows.Count > 0)
            {
                AQIExtention aqiExt;
                string aqiColor = "";
                int spanDays;
                DataTable dtGroupFilter = null;
                if (dtGroup.Columns.Count > 5)
                {
                    dtGroupFilter = dtGroup.DefaultView.ToTable(false, "LST", "DURATIONID", "ITEMID", "VALUE", "AQI", "Parameter");
                }
                else
                {
                    dtGroupFilter = dtGroup;
                }
                foreach (DataRow row in dtGroupFilter.Rows)
                {
                    if (dtGroup.Columns[0].DataType == typeof(DateTime))
                    {
                        DateTime forcast = DateTime.Parse(dtGroup.Rows[0][1].ToString());
                        TimeSpan timeSpan = forcast.Date - DateTime.Parse(row[0].ToString()).Date;
                        spanDays = timeSpan.Days - 1;
                    }
                    else
                    {
                        spanDays = int.Parse(row[0].ToString());
                    }
                    if (row[4].ToString() != "" && row[2].ToString() != "")
                    {
                        aqiExt = new AQIExtention(int.Parse(row[4].ToString()), int.Parse(row[2].ToString()));
                        aqiColor = string.Format("class='{0}'", aqiExt.Color);
                    }
                    if (int.Parse(row[2].ToString()) == 0 && dtGroupFilter.Columns.Count > 5)
                    {
                        //sb.Append(string.Format("{7}{0}{1}{2}{3}:\"<span {5}>{6}</span>/{4}\",", m_BackDays - spanDays, typeID, row[1], 6, row[5], aqiColor, row[4], tag));
                        //sb.Append(string.Format("{7}{0}{1}{2}{3}:\"<span {5}>{6}</span>/{4}\",", m_BackDays - spanDays, 1, row[1], 6, row[5], aqiColor, row[4], tag));
                        sb.Append(string.Format("{7}{0}{1}{2}{3}:\"<span {5}>{6}</span>/{4}\",", m_BackDays - spanDays, 2, row[1], 6, row[5], aqiColor, row[4], tag));
                    }
                    else
                    {
                        //sb.Append(string.Format("{7}{0}{1}{2}{3}:\"{4}/<span {5}>{6}</span>\",", m_BackDays - spanDays, typeID, row[1], row[2], row[3], aqiColor, row[4], tag));
                        //sb.Append(string.Format("{7}{0}{1}{2}{3}:\"{4}/<span {5}>{6}</span>\",", m_BackDays - spanDays, 1, row[1], row[2], row[3], aqiColor, row[4], tag));
                        sb.Append(string.Format("{7}{0}{1}{2}{3}:\"{4}/<span {5}>{6}</span>\",", m_BackDays - spanDays, 2, row[1], row[2], row[3], aqiColor, row[4], tag));
                    }
                }


                //去掉多余的“,”
                if (sb.Length > 1)
                {
                    sb.Remove(sb.Length - 1, 1);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 根据起报时间和模式类型，返回相应模式数据的SQL语句
        /// </summary>
        /// <param name="forecastDate"></param>
        /// <param name="moduleType"></param>
        /// <returns></returns>
        private string CreateModuleSQL(DateTime dtNow, string moduleType)
        {
            string forecastDate;
            string lastForecastDate;
            if (moduleType == "WRF")
            {
                forecastDate = dtNow.Date.ToString("yyyy-MM-dd 00:00:00");
                lastForecastDate = dtNow.AddDays(-1).ToString("yyyy-MM-dd 00:00:00");
            }
            else
            {
                forecastDate = dtNow.AddDays(-1).ToString("yyyy-MM-dd 20:00:00");
                lastForecastDate = dtNow.AddDays(-1).ToString("yyyy-MM-dd 20:00:00");
            }

            string strSQL = String.Format("SELECT TOP 1 LST FROM T_FORECASTGROUP WHERE FORECASTDATE = '{0}' AND MODULE = '{1}' AND EXISTS(SELECT 1 FROM D_DurationTest WHERE (CODE = 1) AND (DM = DURATIONID))", forecastDate, moduleType);
            //作者：张伟锋   日期：2013年08月13日     临时增加代码，为了能够确保可以看到模式预报数据，在当前预报时间下如果没有模式数据，那么获取前一天的模式预报数据

            DataTable dt = m_Database.GetDataTable(strSQL);
            if (dt.Rows.Count == 0)
            {
                strSQL = String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') AS LST, DURATIONID, ITEMID ,VALUE,AQI FROM T_FORECASTGROUP WHERE FORECASTDATE = '{1}' AND MODULE = '{2}' AND EXISTS(SELECT 1 FROM D_DurationTest WHERE (CODE = 1) AND (DM = DURATIONID)) AND LST >='{3}'", dtNow.AddDays(-1).ToString("yyyy-MM-dd 20:00:00"), lastForecastDate, moduleType, dtNow.ToString("yyyy-MM-dd 20:00:00"));

            }
            else
                strSQL = String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') AS LST, DURATIONID, ITEMID ,VALUE,AQI FROM T_FORECASTGROUP WHERE FORECASTDATE = '{1}' AND MODULE = '{2}' AND EXISTS(SELECT 1 FROM D_DurationTest WHERE (CODE = 1) AND (DM = DURATIONID))", dtNow.AddDays(-1).ToString("yyyy-MM-dd 20:00:00"), forecastDate, moduleType);


            return strSQL;
        }

        /// <summary>
        /// 根据开始时间和结束时间返回实况数据的SQL
        /// </summary>
        /// <param name="fromDate"></param>
        /// <param name="toDate"></param>
        /// <returns></returns>
        public string CreateRealtimeSQL(string fromDate, string toDate)
        {
            string strSQL = String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') AS LST, DURATIONID, ITEMID ,VALUE,AQI FROM T_OBSDATAGROUP WHERE LST BETWEEN '{1}' AND '{0}' AND EXISTS(SELECT 1 FROM D_DurationTest WHERE (CODE = 1) AND (DM = DURATIONID))", toDate, fromDate);
            return strSQL;
        }
        /// <summary>
        /// 根据开始时间和结束时间返回实况数据的SQL
        /// </summary>
        /// <param name="fromDate"></param>
        /// <param name="toDate"></param>
        /// <returns></returns>
        public string HistoryCreateRealtimeSQL(string fromDate, string toDate)
        {
            string strSQL = String.Format("SELECT  DATEDIFF(DAY, LST, '{1}')- 1 AS LST, DURATIONID, ITEMID ,VALUE,AQI FROM T_OBSDATAGROUP WHERE LST BETWEEN '{1}' AND '{0}' AND EXISTS(SELECT 1 FROM D_DurationTest WHERE (CODE = 1) AND (DM = DURATIONID))", toDate, fromDate);
            return strSQL;
        }
        private string CreateRealtimeReSeeSQL(string fromDate, string toDate)
        {
            fromDate = DateTime.Parse(fromDate).AddDays(1).ToString("yyyy-MM-dd");
            toDate = DateTime.Parse(toDate).AddDays(1).ToString("yyyy-MM-dd 23:59:59");
            string strSQL = String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') AS LST, DURATIONID, ITEMID ,VALUE,AQI FROM T_OBSDATAGROUP WHERE LST BETWEEN '{1}' AND '{0}' AND EXISTS(SELECT 1 FROM D_DurationTest WHERE (CODE = 1) AND (DM = DURATIONID))", toDate, fromDate);
            return strSQL;
        }

        /// <summary>
        /// 根据预报时间和预报时效，返回综合预报数据的SQL语句
        /// </summary>
        /// <param name="forecastDate"></param>
        /// <param name="period"></param>
        /// <returns></returns>
        private string CreateComSQL(string fromDate, string toDate, string period, string Module)
        {
            string strSQL = String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') AS LST, DURATIONID, ITEMID ,VALUE,AQI FROM T_FORECASTGROUP WHERE LST BETWEEN '{1}' AND '{0}' AND MODULE = '{3}' AND PERIOD = {2} AND EXISTS(SELECT 1 FROM D_DurationTest WHERE (CODE = 1) AND (DM = DURATIONID))", toDate, fromDate, period, Module);
            return strSQL;
        }
        private string CreateComReSeeSQL(string fromDate, string toDate, string period, string Module)
        {
            fromDate = DateTime.Parse(fromDate).AddDays(1).ToString("yyyy-MM-dd");
            toDate = DateTime.Parse(toDate).AddDays(1).ToString("yyyy-MM-dd 23:59:59");
            string strSQL = String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') AS LST, DURATIONID, ITEMID ,VALUE,AQI FROM T_FORECASTGROUP WHERE LST BETWEEN '{1}' AND '{0}' AND MODULE = '{3}' AND PERIOD = {2} AND EXISTS(SELECT 1 FROM D_DurationTest WHERE (CODE = 1) AND (DM = DURATIONID))", toDate, fromDate, period, Module);
            return strSQL;
        }
        private string CreateComReviseSQL(string fromDate, string toDate, string period, string Module)
        {
            int hour = int.Parse(DateTime.Now.Hour.ToString());
            string endDate = DateTime.Parse(toDate).ToString("yyyy-MM-dd 05:59:59");
            string strSQL = "";
            strSQL = String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') AS LST, DURATIONID, ITEMID ,VALUE,AQI FROM T_FORECASTGROUP WHERE LST BETWEEN '{1}' AND '{0}' AND MODULE = '{3}' AND PERIOD = {2} AND EXISTS(SELECT 1 FROM D_DurationTest WHERE (CODE = 1) AND (DM = DURATIONID))", endDate, fromDate, period, Module);
            return strSQL;
        }
        /// <summary>
        /// 根据起报时间返回历史预报数据
        /// </summary>
        /// <param name="forecastDate"></param>
        /// <returns></returns>
        private string CreateHistoryComSQL(string forecastDate, string Module)
        {
            string strSQL = "";
            int hour = int.Parse(DateTime.Now.Hour.ToString());
            strSQL = String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') - 1 AS LST, DURATIONID, ITEMID ,VALUE,AQI FROM T_FORECASTGROUP WHERE FORECASTDATE = '{0}' AND MODULE = '{1}' AND EXISTS(SELECT 1 FROM D_DurationTest WHERE (CODE = 1) AND (DM = DURATIONID))", forecastDate, Module);
            return strSQL;
        }
        private string CreateHistoryComReviseSQL(string forecastDate, string Module)
        {
            //DATEDIFF(DAY, LST, '{0}') - 1，这里的“-1”，是前台表格的行号偏移量
            string dt;
            dt = DateTime.Parse(forecastDate).AddDays(1).ToString("yyyy-MM-dd HH:00:00");
            string strSQL = String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') AS LST, DURATIONID, ITEMID ,VALUE,AQI FROM T_FORECASTGROUP WHERE FORECASTDATE = '{0}' AND LST>='{2}' AND MODULE = '{1}' AND EXISTS(SELECT 1 FROM D_DurationTest WHERE (CODE = 1) AND (DM = DURATIONID))", forecastDate, Module, dt);
            return strSQL;
        }
        public string SelectComReviseSQL(string hour, string period, string Module, string moduleStyle)
        {
            string strSQL = "";
            DateTime dtNow = GetManualForecastDate(hour);
            string sb = "";
            if (Module == "Manual")
            {
                strSQL = CreatComforecastSQL("0", dtNow.ToString("yyyy-MM-dd HH:00:00"), "Modify");
                DataTable dt = m_Database.GetDataTable(strSQL);
                if (dt.Rows.Count > 0)
                {
                    sb = GetForecast("0", hour, period, "Modify", "gengZ", moduleStyle);

                }
                else
                {
                    strSQL = CreatComforecastSQL("0", dtNow.ToString("yyyy-MM-dd HH:00:00"), Module);
                    dt = m_Database.GetDataTable(strSQL);
                    if (dt.Rows.Count > 0)
                    {
                        sb = GetForecast("0", hour, period, Module, "zhonghe", moduleStyle);
                    }
                    else
                    {
                        sb = GetForecast("1", hour, period, Module, "gengZ", moduleStyle);
                    }

                }
            }
            else
            {
                strSQL = CreatComforecastSQL("0", dtNow.ToString("yyyy-MM-dd HH:00:00"), "SMCModify");
                DataTable dt = m_Database.GetDataTable(strSQL);
                if (dt.Rows.Count > 0)
                {
                    sb = GetForecast("0", hour, period, "SMCModify", "gengZ", moduleStyle);

                }
                else
                {
                    strSQL = CreatComforecastSQL("0", dtNow.ToString("yyyy-MM-dd HH:00:00"), Module);
                    dt = m_Database.GetDataTable(strSQL);
                    if (dt.Rows.Count > 0)
                    {
                        sb = GetForecast("0", hour, period, Module, "gengZ", moduleStyle);
                    }
                    else
                    {
                        sb = GetForecast("1", hour, period, Module, "gengZ", moduleStyle);
                    }

                }

            }

            return sb;

        }
        public string CreatComforecastSQL(string flag, string forecastDate, string Module)
        {
            string sb = "";
            if (flag == "0")
                sb = CreateHistoryComSQL(forecastDate, Module);
            else
            {
                forecastDate = DateTime.Parse(forecastDate).AddDays(-1).ToString("yyyy-MM-dd HH:00:00");
                sb = CreateHistoryComReviseSQL(forecastDate, Module);
            }
            return sb;

        }

        //2015年11月11日  闫海涛  修改为起报时间为昨天
        public string BuildPreconsation(string forecastDate)
        {
            DateTime dtNow = DateTime.Now.Date.AddHours(18);
            if (forecastDate != "")
                dtNow = DateTime.Parse(forecastDate).AddHours(20);
            string forecastDateTime = dtNow.ToString("yyyy-MM-dd HH:00:00");
            Entity entity = new Entity(m_Database, "Forecast");
            string strSQL = string.Format("FORECASTDATE = '{0}'", dtNow);//起报时间是每天的18点

            //strSQL = entity.BuildQuerySQL(strSQL, "ManualCenter");

            //DataTable datePreTable = m_Database.GetDataTable(strSQL);
            StringBuilder sb = new StringBuilder("{");
            //strSQL = string.Format("FORECASTDATE = '{0}'", dtNow);//起报时间是每天的18点
            //strSQL = entity.BuildQuerySQL(strSQL, "Public");
            //DataTable formJson = m_Database.GetDataTable(strSQL);
            //创建表单json
            string json = "";
            //if (formJson.Rows.Count > 0)
            //{
            //    if (datePreTable.Rows.Count > 0)
            //    {
            //        datePreTable.Rows[0]["H056"] = formJson.Rows[0]["H056"];
            //        datePreTable.Rows[0]["H052"] = formJson.Rows[0]["H052"];
            //        datePreTable.Rows[0]["H053"] = formJson.Rows[0]["H053"];
            //        datePreTable.Rows[0]["H066"] = formJson.Rows[0]["H066"];
            //        datePreTable.Rows[0]["H062"] = formJson.Rows[0]["H062"];
            //        datePreTable.Rows[0]["H063"] = formJson.Rows[0]["H063"];
            //        json = GetForecastJSON(datePreTable);
            //    }
            //    else
            //    {
            //        json = GetForecastJSON(formJson);
            //    }
            //}
            ////创建表单json

            //sb.Append(json);
            //if (json != "")
            //    sb.Append(",");

            //strSQL = String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') - 1 AS LST, DURATIONID, ITEMID ,VALUE,AQI,Parameter FROM T_ForecastGroup WHERE FORECASTDATE = '{0}' AND Module='ManualSubmit';", forecastDateTime);
            //strSQL = strSQL + String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') - 1 AS LST, DURATIONID, ITEMID ,VALUE,AQI,Parameter FROM T_ForecastGroup WHERE FORECASTDATE = '{0}' AND Module='SMCSubmit';", forecastDateTime);
            //strSQL = strSQL + String.Format("SELECT  DATEDIFF(DAY, LST, '{0}') - 1 AS LST, DURATIONID, ITEMID ,VALUE,AQI,Parameter FROM T_ForecastGroup WHERE FORECASTDATE = '{0}' AND Module='ManualCenter';", forecastDateTime);

            //strSQL = String.Format("select DATEDIFF(DAY, LST,a.maxtime) AS DIFF, DURATIONID, ITEMID ,VALUE,AQI from T_ForecastSite , (select MAX(ForecastDate) maxtime from T_ForecastSite)  a WHERE Site='58367'");   
            strSQL = String.Format("select DATEDIFF(DAY, LST,a.maxtime) AS DIFF, DURATIONID, ITEMID ,VALUE,AQI from T_ForecastSite , (select MAX(ForecastDate) maxtime from T_ForecastSite)  a WHERE ForecastDate = a.maxtime AND Site='58367'");

            DataSet ds = m_Database.GetDataset(strSQL);
            string style = "";
            //DataTable dTable = m_Database.GetDataTable(strSQL);
            if (ds.Tables.Count > 0)
            {
                for (int i = 0; i < ds.Tables.Count; i++)
                {
                    DataTable dTable = ds.Tables[i];
                    if (i == 2)
                        style = "H";
                    else
                        style = "P";

                    //生成实况，综合预报，模式数据的json
                    if (dTable.Rows.Count > 0)
                    {
                        //创建json，便于前台赋值
                        //json = GetGroupJSON(dTable, i % 2 + 1, style);//实况typeID = 0;//综合预报typeID = 1//模式typeID = 2

                        //闫海涛修改
                        json = GetGroupJSONSingle(dTable, i % 2 + 1, style);

                        if (json != "")
                        {
                            sb.Append(json);
                            sb.Append(",");
                        }
                    }
                }

            }
            sb.Append(string.Format("nowDateTime:'{0}'", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            if (sb.Length > 1)
            {
                sb.Append("}");
            }
            else
                sb.Length = 0;

            return sb.ToString();

        }
        ///////
        /////// 创建汇总数据
        ///////
        public string BuildCollect(string forecastDate, string postJson, string Module)
        {
            DateTime dtForecastDate = DateTime.Parse(forecastDate);
            dtForecastDate = dtForecastDate.AddHours(18);
            StringBuilder sb = new StringBuilder("{");
            //Module = "Module";
            DataTable dTable = CaculateContent(dtForecastDate, postJson, Module);
            if (dTable.Rows.Count > 0)
            {
                //创建json，便于前台赋值
                string json = GetGroupJSON(dTable, 1, "PH");//实况typeID = 0;//综合预报typeID = 1//模式typeID = 2

                if (json != "")
                {
                    sb.Append(json);
                    sb.Append(",");
                }

            }
            if (sb.Length > 1)
            {
                sb.Remove(sb.Length - 1, 1);
                sb.Append("}");
            }
            else
                sb.Length = 0;

            return sb.ToString();

        }
        /// <summary>
        /// 创建预报预览，并根据当前的ID判断是否需要计算日平均，并计算日平均
        /// </summary>
        /// <param name="postJson"></param>
        /// <param name="rowID"></param>
        /// <param name="itemID"></param>
        /// <returns></returns>
        public string BuildPreview(string forecastDate, string postJson, string divID, string itemID, string detail, string Module)
        {
            try
            {
                DateTime dtForecastDate = GetManualForecastDate(forecastDate);

                DataTable tbContent = CaculateContent(dtForecastDate, postJson, Module);

                //24小时预报预览
                StringBuilder sb = new StringBuilder("{H09:\"");

                //夜间
                AQIExtention aqiExt = ConvertAQIDescription(tbContent, dtForecastDate, 6, 24);

                if (aqiExt != null)
                    sb.AppendFormat("{0}夜间，{1}，{2}，{3}；", dtForecastDate.ToString("M月d日"), aqiExt.AQI, aqiExt.Quality, aqiExt.FirstItem);
                //上午
                aqiExt = ConvertAQIDescription(tbContent, dtForecastDate, 2, 24);
                if (aqiExt != null)
                    sb.AppendFormat("{0}上午，{1}，{2}，{3}，", dtForecastDate.AddDays(1).ToString("d日"), aqiExt.AQI, aqiExt.Quality, aqiExt.FirstItem);
                //下午
                aqiExt = ConvertAQIDescription(tbContent, dtForecastDate, 3, 24);
                if (aqiExt != null)
                    sb.AppendFormat("下午，{0}，{1}，{2}。", aqiExt.AQI, aqiExt.Quality, aqiExt.FirstItem);


                //48小时预报预览
                sb.Append("\",H10:\"");
                //夜间
                aqiExt = ConvertAQIDescription(tbContent, dtForecastDate, 6, 48);
                if (aqiExt != null)
                    sb.AppendFormat("{0}夜间，{1}，{2}，{3}；", dtForecastDate.AddDays(1).ToString("M月d日"), aqiExt.AQI, aqiExt.Quality, aqiExt.FirstItem);
                //上午
                aqiExt = ConvertAQIDescription(tbContent, dtForecastDate, 2, 48);
                if (aqiExt != null)
                    sb.AppendFormat("{0}上午，{1}，{2}，{3}，", dtForecastDate.AddDays(2).ToString("d日"), aqiExt.AQI, aqiExt.Quality, aqiExt.FirstItem);
                //下午
                aqiExt = ConvertAQIDescription(tbContent, dtForecastDate, 3, 48);
                if (aqiExt != null)
                    sb.AppendFormat("下午，{0}，{1}，{2}。", aqiExt.AQI, aqiExt.Quality, aqiExt.FirstItem);


                //24小时预报预览
                //夜间
                sb.Append("\",PH10:\"");
                StringBuilder sm = new StringBuilder();
                string firstItem = "";
                aqiExt = ConvertAQIDescription(tbContent, dtForecastDate, 6, 24);
                if (aqiExt != null)
                {
                    if (Module != "Modify" && Module != "SMCModify")//预报更正
                    {
                        sb.AppendFormat("预计{0}夜间，分段指数为{1}；", dtForecastDate.ToString("M月d日"), ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, true));
                    }
                    if (ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, true).Split('，')[1].ToString() == "优")
                    {
                        firstItem = "-";
                    }
                    else
                    {
                        firstItem = aqiExt.FirstPItemNoByGrade;
                    }

                    sm.AppendFormat("{0}夜间,{1},{2},{3},", dtForecastDate.ToString("d日"), ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, true).Split('，')[0].ToString(), ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, true).Split('，')[1].ToString() == "" ? "-" : ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, true).Split('，')[1].ToString(), stringFormat(firstItem));
                }
                else
                {
                    sm.AppendFormat("{0}夜间,{1},{2},{3},", dtForecastDate.ToString("d日"), "/", "/", "/");
                }
                //上午
                aqiExt = ConvertAQIDescription(tbContent, dtForecastDate, 2, 24);
                if (aqiExt != null)
                {
                    if (Module != "Modify" && Module != "SMCModify")
                    {
                        sb.AppendFormat("{0}上午，{1}；", dtForecastDate.AddDays(1).ToString("d日"), ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, false));
                    }
                    else
                    {
                        sb.AppendFormat("预计{0}上午，分段指数为{1}；", dtForecastDate.AddDays(1).ToString("M月d日"), ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, true));
                    }
                    if (ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, true).Split('，')[1].ToString() == "优")
                    {
                        firstItem = "-";
                    }
                    else
                    {
                        firstItem = aqiExt.FirstPItemNoByGrade;
                    }
                    sm.AppendFormat("{0}上午,{1},{2},{3},", dtForecastDate.AddDays(1).ToString("d日"), ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, true).Split('，')[0].ToString(), ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, true).Split('，')[1].ToString() == "" ? "-" : ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, true).Split('，')[1].ToString(), stringFormat(firstItem));
                }
                else
                {
                    sm.AppendFormat("{0}上午,{1},{2},{3},", dtForecastDate.AddDays(1).ToString("d日"), "/", "/", "/");
                }
                //下午
                aqiExt = ConvertAQIDescription(tbContent, dtForecastDate, 3, 24);
                if (aqiExt != null)
                {
                    sb.AppendFormat("下午，{0}。", ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, false));
                    if (ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, true).Split('，')[1].ToString() == "优")
                    {
                        firstItem = "-";
                    }
                    else
                    {
                        firstItem = aqiExt.FirstPItemNoByGrade;
                    }
                    sm.AppendFormat("{0}下午,{1},{2},{3},", dtForecastDate.AddDays(1).ToString("d日"), ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, true).Split('，')[0].ToString(), ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, true).Split('，')[1].ToString() == "" ? "-" : ParseAQIForSM(aqiExt.AQI, aqiExt.FirstPItemNoByGrade, true).Split('，')[1].ToString(), stringFormat(firstItem));
                }
                else
                {
                    sm.AppendFormat("{0}下午,{1},{2},{3},", dtForecastDate.AddDays(1).ToString("d日"), "/", "/", "/");

                }
                sb.Append("\"");
                sm.Append(detail);
                sm.Append(",上海市环境监测中心 上海中心气象台,");
                sm.AppendFormat("{0}{1}时发布", dtForecastDate.ToString("M月d日"), DateTime.Now.Hour.ToString());
                string[] message = sm.ToString().Split(',');
                string existsSQL, updateSQL, insertSQL;
                existsSQL = @"SELECT foreDate FROM tb_AirForecast WHERE foreDate='" + dtForecastDate.ToString("yyyy年M月d日") + "'";
                updateSQL = @"UPDATE tb_AirForecast SET Seg1='" + message[0].ToString() + "',AQI1='" + message[1].ToString() + "',Grade1='" + message[2].ToString() + "',Param1='" + message[3].ToString() + "',Seg2='" + message[4].ToString() + "',AQI2='" + message[5].ToString() + "',Grade2='" + message[6].ToString() + "',Param2='" + message[7].ToString() + "',Seg3='" + message[8].ToString() + "',AQI3='" + message[9].ToString() + "',Grade3='" + message[10].ToString() + "',Param3='" + message[11].ToString() + "',Detail='" + message[12].ToString() + "',Sign='" + message[13].ToString() + "',publishTime='" + message[14].ToString() + "' WHERE foreDate='" + dtForecastDate.ToString("yyyy年M月d日") + "'";
                insertSQL = @"INSERT INTO tb_AirForecast VALUES('" + dtForecastDate.ToString("yyyy年M月d日") + "', '" + message[0].ToString() + "','" + message[1].ToString() + "','" + message[2].ToString() + "','" + message[3].ToString() + "','" + message[4].ToString() + "','" + message[5].ToString() + "','" + message[6].ToString() + "','" + message[7].ToString() + "','" + message[8].ToString() + "','" + message[9].ToString() + "','" + message[10].ToString() + "','" + message[11].ToString() + "','" + message[12].ToString() + "','" + message[13].ToString() + "','" + message[14].ToString() + "')";
                m_Database.Execute(existsSQL, updateSQL, insertSQL);


                //计算日平均
                int period = 24;
                double rowsDate;
                if (itemID != "")
                {
                    string rowID = divID.Substring(1, 1);
                    if (rowID == "5")
                        period = 48;

                    string strFilter = string.Format("ForecastDate = '{0}' AND durationID = {1} AND PERIOD = {2} AND ITEMID = {3}", dtForecastDate, 7, period, itemID);
                    DataRow[] rows = tbContent.Select(strFilter);
                    if (rows[0][6] == DBNull.Value)//指定模式（pm2.5 pm10）下指定时间段（7指全天，1上半夜）（24小时或者48小时）下的AQI值
                        sb.AppendFormat(",{0}:''", divID);//为空也要加上“‘’”否则，前台序列化会报错。
                    else
                    {
                        aqiExt = new AQIExtention(int.Parse(rows[0][6].ToString()), int.Parse(itemID));
                        string aqiColor = string.Format("class='{0}'", aqiExt.Color);
                        string str = rows[0][5].ToString();//指定模式（pm2.5 pm10）下指定时间段（7指全天，1上半夜）（24小时或者48小时）下的VALUE值
                        rowsDate = Math.Round(double.Parse(str), 1);
                        str = rowsDate.ToString("f1");
                        sb.AppendFormat(",{0}:\"{1}/<span {2}>{3}</span>\"", divID, str, aqiColor, rows[0][6]);
                    }
                }

                sb.Append("}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                m_Log.Error("BuildPreview", ex);
                return "";
            }

        }
        public string stringFormat(string items)
        {
            string returnItem = "-";
            switch (items)
            {
                case "PM2.5":
                    returnItem = "PM<sub>2.5</sub>"; break;
                case "PM10":
                    returnItem = "PM<sub>10</sub>"; break;
                case "NO2":
                    returnItem = "NO<sub>2</sub>"; break;
                case "O3":
                    returnItem = "O<sub>3</sub>"; break;
            }
            return returnItem;
        }
        /// <summary>
        /// 按照短信要求，重新组织AQI的值，标准如下：
        /// 1、在预报出的AQI基础上加正负10。
        /// 2、对范围进行修正，原则是个位数凑成5或0，凑范围的原则为就近原则，如72-92凑成70-90，73-93凑成75-95。
        /// 3、修正后还有以下几种特殊情况，
        ///     a)         0-20修正为1-20；
        ///     b)         50-70修正为55-75
        ///     c)         100-120修正为105-125
        ///     d)         150-170修正为155-175
        ///     e)         200-220修正为205-225
        ///     f)          300-320修正为305-325
        /// 4、修正后，对范围的两个至分别求等级，如果两个都在一个等级内，之描述一个等级即可，如果在两个等级内，可由低到高描述为**到**，如良到轻度污染。
        /// </summary>
        /// <param name="aqi"></param>
        /// <returns></returns>
        private string ParseAQIForSM(int aqi, string firstParameter, bool showDescription)
        {
            int i = aqi % 10;

            string description = "首要污染物";
            if (showDescription == false)
                description = "";
            description = "，" + description + firstParameter;

            //2、对范围进行修正，原则是个位数凑成5或0，凑范围的原则为就近原则，如72-92凑成70-90，73-93凑成75-95。
            if (i < 3)
                i = 0;
            else if (i > 7)
                i = 10;
            else
                i = 5;
            //1、在预报出的AQI基础上加正负10。
            int fAQI = aqi / 10 * 10 - 10 + i;
            if (fAQI <= 0)
                fAQI = 1;
            int tAQI = aqi / 10 * 10 + 10 + i;

            //3、修正后还有以下几种特殊情况，
            if (fAQI == 50 || fAQI == 100 || fAQI == 150 || fAQI == 200 || fAQI == 300)
            {
                fAQI = fAQI + 5;
                tAQI = tAQI + 5;
            }

            //4、修正后，对范围的两个至分别求等级，如果两个都在一个等级内，之描述一个等级即可，如果在两个等级内，可由低到高描述为**到**，如良到轻度污染。
            AQIExtention fAqiExt = new AQIExtention(fAQI);
            AQIExtention tAqiExt = new AQIExtention(tAQI);
            string strGrade = fAqiExt.Quality;

            if (strGrade != tAqiExt.Quality)
            {
                strGrade = string.Format("{0}到{1}", strGrade, tAqiExt.Quality);
            }
            else if (strGrade == "优")//优的情况下，不显示首页污染物
                description = "";
            if (strGrade == "轻度污染到中度污染")
                strGrade = "轻度到中度污染";
            if (strGrade == "中度污染到重度污染")
                strGrade = "中度到重度污染";
            if (strGrade == "重度污染到严重污染")
                strGrade = "重度到严重污染";

            string aqiSM = string.Format("{0}-{1}，{2}{3}", fAQI, tAQI, strGrade, description);

            return aqiSM;
        }




        /// <summary>
        /// 根据数据返回指定时间的AQI描述
        /// </summary>
        /// <param name="tbContent">预报内容</param>
        /// <param name="dtForecastDate">起报时间</param>
        /// <param name="durationID">分段ID</param>
        /// <param name="period">预报时效</param>
        /// <returns></returns>
        private AQIExtention ConvertAQIDescription(DataTable tbContent, DateTime dtForecastDate, int durationID, int period)
        {

            string strFilter = string.Format("ForecastDate = '{0}' AND durationID = {1} AND PERIOD = {2}", dtForecastDate, durationID, period);
            DataRow[] rows = tbContent.Select(strFilter, "AQI DESC");
            if (rows[0][6] == DBNull.Value)
                return null;
            int items = int.Parse(rows[0][4].ToString());
            int AQI = int.Parse(rows[0][6].ToString());
            if (items == 5)
            {
                if (int.Parse(rows[1][4].ToString()) == 0)
                {
                    items = int.Parse(rows[2][4].ToString());
                    AQI = int.Parse(rows[2][6].ToString());
                }
                else
                {
                    items = int.Parse(rows[1][4].ToString());
                    AQI = int.Parse(rows[1][6].ToString());
                }
            }

            AQIExtention aqiExt = new AQIExtention(AQI, items);
            return aqiExt;


        }

        /// <summary>
        /// 保存综合预报，分开处理预报表单和预报内容
        /// </summary>
        /// <param name="postJson"></param>
        /// <returns></returns>
        public string SaveEdits(string postJson, string Module)
        {
            string forecastDate;
            string Flag = "";
            string[] parts = postJson.Split(';');
            if (Module == "Manual" || Module == "SMC" || Module == "SMCModify" || Module == "Modify")
                Flag = "Former";
            else if (Module == "ManualCenter" || Module == "SMCCenter")
                Flag = "ManualCenter";
            else
                Flag = Module;

            //处理预报表单
            try
            {
                forecastDate = SaveForm(parts[0], Flag);
                forecastDate = SaveForm(parts[0], "Public");
                //处理预报内容
                if (parts.Length > 1)
                    SaveContent(forecastDate, parts[1], Module);
                return "{success:true}";
            }
            catch (Exception ex)
            {
                m_Log.Error("SaveEdits", ex);
                return ex.ToString();
            }


        }
        public string SaveComForecastReSee(string forecastDate, string wether24, string wether48, string polution24, string polution48)
        {
            string flag = "Former";
            string forecastTime = DateTime.Parse(forecastDate).ToString("yyyy-MM-dd 18:00:00");
            string existsSQL, updateSQL, insertSQL;
            try
            {
                existsSQL = "SELECT ForecastDate FROM T_Forecast WHERE ForecastDate='" + forecastTime + "' AND Flag='" + flag + "'";
                updateSQL = "UPDATE T_Forecast SET UpdateDate=GETDATE(), WeatherReview24='" + wether24 + "',WeatherReview48='" + wether48 + "',PolutionReview24='" + polution24 + "',PolutionReview48='" + polution48 + "' WHERE ForecastDate='" + forecastTime + "' AND Flag='" + flag + "'";
                insertSQL = "INSERT INTO T_Forecast(ForecastDate,WeatherReview24,WeatherReview48,PolutionReview24,PolutionReview48,Flag,UpdateDate) VALUES('" + forecastTime + "', '" + wether24 + "', '" + wether48 + "', '" + polution24 + "', '" + polution48 + "','" + flag + "',GetDate())";
                m_Database.Execute(existsSQL, updateSQL, insertSQL);
                return "保存成功";
            }
            catch (Exception ex)
            {
                m_Log.Error("SaveComForecastReSee", ex);
                return ex.ToString();
            }

        }



        /// <summary>
        /// 提交表单
        /// </summary>
        /// <param name="formContent"></param>
        /// <returns></returns>
        private string SaveForm(string formContent, string Module)
        {
            try
            {
                Entity entity = new Entity(m_Database, "Forecast");
                FilterOV filterOV = new FilterOV();
                string[] parts = formContent.Split(',');

                string[] keyValue = parts[0].Split(':');
                PropertyOV propertyOV = entity.GetPropertyOV(keyValue[0]);
                string formDate = GetManualForecastDate(keyValue[1]).ToString("yyyy-MM-dd HH:mm:ss");//综合预报起报时间

                propertyOV.ShowValue = formDate;
                filterOV.Add(propertyOV);
                string whereCause = string.Format(" WHERE {0} = '{1}' AND Flag='{2}'", propertyOV.Name, formDate, Module);
                string existsSQL = string.Format("SELECT ForecastDate FROM {0} {1}", entity.TableName, whereCause);

                for (int i = 1; i < parts.Length; i++)
                {
                    keyValue = parts[i].Split(':');
                    propertyOV = entity.GetPropertyOV(keyValue[0]);
                    propertyOV.ShowValue = keyValue[1];
                    filterOV.Add(propertyOV);
                }
                entity.EntityState = EntityStateContants.esUpdate;
                entity.SaveHistory = true;
                string updateSQL = entity.BuildSQL(filterOV) + whereCause;
                string formatStrSQL = string.Format("SET Flag='{0}',", Module);
                updateSQL = updateSQL.Replace("SET", formatStrSQL);
                entity.EntityState = EntityStateContants.esInsert;
                string insertSQL = entity.BuildSQL(filterOV);
                int indexStart = insertSQL.IndexOf(')');
                string tempStr = insertSQL.Insert(indexStart, ",Flag");
                string formatStr = string.Format(",'{0}'", Module);
                insertSQL = tempStr.Insert(tempStr.Length - 1, formatStr);
                //存在就更新，不存在就插入
                int ret = m_Database.Execute(existsSQL, updateSQL, insertSQL);


                if (ret == 1)
                    return formDate;
                else
                    return "";
            }
            catch (Exception ex)
            {
                m_Log.Error("SaveForm", ex);
                return ex.ToString();
            }
        }

        /// <summary>
        /// 把预报信息存入数据库，通过分析数据，得出4段时效
        /// 数据格式说明："H3141:1/2"，
        /// H：标签标识，即需要展示和编辑的标签
        /// 3：表示行号，用于计算综合预报的预报时间，
        /// 1：表示数据类型，实况typeID = 0;//综合预报typeID = 1//模式typeID = 2
        /// 4：表示时段，分为1（0-6h）,2（6-12h）,3（12-18h）,4（18-24）,5（0-6h）,6（6-18h）,7（0-24h），存储在字典表D_DurationTest表中
        /// 1：表示污染物，1（PM2.5）,2（PM10）,3（NO2）,4（03-1h）,5（03-8h），存储在字典表D_Item表中
        /// :：表示数据分隔，前部分是数据的描述，后面部分是污染物浓度和AQI值
        /// /：污染物浓度和AQI值的分隔标识
        /// 作者：张伟锋      日期：2013年06月30日      
        /// </summary>
        /// <param name="forecastContent">起报时间</param>
        /// <returns>入库成功返回true，否则返回false</returns>
        private bool SaveContent(string forecastDate, string forecastContent, string Module)
        {
            string strSQL;
            try
            {
                strSQL = string.Format("DELETE T_ForecastGroup_temp WHERE FORECASTDATE = '{0}' AND MODULE = '{1}'; INSERT INTO T_ForecastGroup_temp SELECT LST,ForecastDate,PERIOD,Module,durationID,ITEMID,Value,AQI,GROUPID,Parameter FROM  T_ForecastGroup WHERE FORECASTDATE = '{0}' AND MODULE = '{1}'", forecastDate, Module);
                m_Database.Execute(strSQL);
                strSQL = string.Format("DELETE T_ForecastGroup WHERE FORECASTDATE = '{0}' AND MODULE = '{1}'", forecastDate, Module);
                DataTable dt = CaculateContent(DateTime.Parse(forecastDate), forecastContent, Module);
                m_Database.Execute(strSQL);//删除已有记录
                return m_Database.BulkCopy(dt);
            }
            catch (Exception ex)
            {
                m_Log.Error("SaveContent", ex);
                return false;
            }

        }

        /// <summary>
        /// 计算预报内容，并返回表格
        /// </summary>
        /// <param name="forecastDate">起报时间</param>
        /// <param name="forecastContent">预报内容</param>
        /// <returns></returns>
        private DataTable CaculateContent(DateTime forecastDate, string forecastContent, string Module)
        {
            DateTime startDate = forecastDate.Date;
            DateTime lst;


            DataTable dt = new DataTable("T_ForecastGroup");
            dt.Columns.Add("LST", typeof(DateTime));
            dt.Columns.Add("ForecastDate", typeof(DateTime));
            dt.Columns.Add("PERIOD", typeof(int));

            dt.Columns.Add("durationID", typeof(int));
            dt.Columns.Add("ITEMID", typeof(int));
            dt.Columns.Add("Value", typeof(double));
            dt.Columns.Add("AQI", typeof(int));
            dt.Columns.Add("GROUPID", typeof(int));
            dt.Columns.Add("Module", typeof(string));
            dt.Columns.Add("Parameter", typeof(string));

            string[] parts = forecastContent.Split(',');
            string[] keyValue;
            int rowIndex = 0;
            int itemID;
            int durationID;
            int period = 24;
            float value;
            int AQI;

            string strSQL = "SELECT DM,MC FROM D_DurationTest";
            DataTable timePeriod = m_Database.GetDataTable(strSQL);
            for (int i = 0; i < parts.Length - 1; i++)
            {
                DataRow newRow = dt.NewRow();
                keyValue = parts[i].Split(':');
                //获取行号，并计算此行的预报日期
                rowIndex = int.Parse(keyValue[0].Substring(1, 1));
                lst = startDate.AddDays(rowIndex - m_BackDays - 1);

                //获取分段ID
                durationID = int.Parse(keyValue[0].Substring(3, 1));
                newRow[3] = durationID;

                string[] durationSpan = GetDurationSpan(timePeriod, durationID);
                lst = lst.AddHours(int.Parse(durationSpan[0]));
                newRow[0] = lst;
                newRow[1] = forecastDate;

                //与开始时间相比获取预报时效

                TimeSpan dateDiff = lst.Subtract(forecastDate);
                if (dateDiff.TotalHours >= 24)
                    period = 48;
                newRow[2] = period;

                //获取污染物类型ID
                itemID = int.Parse(keyValue[0].Substring(4, 1));
                newRow[4] = itemID;


                //获取污染物浓度
                if (keyValue[1].IndexOf('/') > 0)
                {
                    keyValue = keyValue[1].Split('/');
                    value = float.Parse(keyValue[0]);
                    AQI = int.Parse(keyValue[1]);
                    newRow[5] = Math.Round(value, 1);
                    newRow[6] = AQI;
                }
                else
                {
                    newRow[5] = DBNull.Value;
                    newRow[6] = DBNull.Value;
                }
                newRow[7] = 2;
                newRow[8] = Module;
                newRow[9] = DBNull.Value;
                dt.Rows.Add(newRow);
            }

            DataTable otherTable = CaculateOthers(dt, timePeriod, forecastDate, Module);
            dt.Merge(otherTable);
            DataTable maxTable = CaculateMax(dt, timePeriod, forecastDate, Module);
            dt.Merge(maxTable);


            return dt;
        }


        private DataTable CaculateMax(DataTable dt, DataTable timePeriod, DateTime forecastDate, string Module)
        {
            int maxAQI, preItems = 0;
            string filter, paraments = "";
            DataRow[] rows;
            DataTable tmpTable = dt.Clone();

            string strSQL = "SELECT DM,MC FROM D_ITEM";
            DataTable items = m_Database.GetDataTable(strSQL);
            for (int i = 1; i < 8; i++)
            {
                int period = 24;
                for (int j = 0; j < 2; j++)
                {
                    DataRow newRow = tmpTable.NewRow();
                    if (period == 24)
                        period = 48;
                    else
                        period = 24;
                    filter = string.Format("durationID = {0} AND PERIOD = '{1}'", i, period);
                    maxAQI = int.Parse(dt.Compute("max(AQI)", filter).ToString() == "" ? "0" : dt.Compute("max(AQI)", filter).ToString());
                    if (maxAQI == 0)
                    {
                        filter = string.Format("durationID = {0} AND PERIOD = '{1}'", i, period);
                    }
                    else
                    {
                        filter = string.Format("durationID = {0} AND PERIOD = '{1}' AND AQI={2}", i, period, maxAQI);
                    }
                    rows = dt.Select(filter);

                    newRow[0] = DateTime.Parse(rows[0][0].ToString());
                    newRow[1] = forecastDate;
                    newRow[2] = period;
                    newRow[3] = i;
                    newRow[4] = 0;
                    paraments = "";
                    for (int m = 0; m < rows.Length; m++)
                    {

                        preItems = int.Parse(rows[m][4].ToString());
                        filter = string.Format("DM = {0}", preItems);
                        DataRow[] itemsDataRow = items.Select(filter);
                        paraments = paraments + "   " + itemsDataRow[0][1].ToString();

                    }
                    if (maxAQI == 0)
                    {
                        newRow[5] = DBNull.Value;
                        newRow[6] = DBNull.Value;
                        newRow[9] = DBNull.Value;
                    }
                    else
                    {
                        newRow[5] = Math.Round(double.Parse(rows[0][5].ToString()), 1);
                        newRow[6] = maxAQI;
                        newRow[9] = paraments;

                    }
                    newRow[7] = 2;
                    newRow[8] = Module;
                    tmpTable.Rows.Add(newRow);

                }
            }
            return tmpTable;

        }


        private DataTable CaculateOthers(DataTable dt, DataTable timePeriod, DateTime forecastDate, string Module)
        {
            //计算白天（6-20h）：5、夜晚（20-6h）：6、全天（0-24h）：7 的数据

            //获取污染物的ID
            DataTable dicItem = dt.DefaultView.ToTable(true, "ITEMID");


            DataTable tmpTable = dt.Clone();

            //存储分段ID
            int durationID = 5;

            DateTime stopDatetime;
            DateTime fromDatetime;
            DateTime endDatetime;
            string[] durationSpan;
            foreach (DataRow r in dicItem.Rows)
            {
                //对于每一种污染物需要初始化
                fromDatetime = forecastDate.Date;
                stopDatetime = forecastDate.AddDays(2);//2段时效，48小时预报
                durationSpan = GetDurationSpan(timePeriod, durationID);//获取分段时间范围
                fromDatetime = fromDatetime.AddHours(int.Parse(durationSpan[0]));
                endDatetime = fromDatetime.Date.AddHours(int.Parse(durationSpan[1]));
                while (fromDatetime < stopDatetime.Date)
                {

                    DataRow newRow = AddNewRow(dt, tmpTable, forecastDate, fromDatetime, endDatetime, durationID, r[0], timePeriod, Module);
                    tmpTable.Rows.Add(newRow);
                    fromDatetime = fromDatetime.AddDays(1);
                    endDatetime = endDatetime.AddDays(1);
                }


                //1段时效，48小时预报
                fromDatetime = forecastDate.Date.AddDays(1);
                stopDatetime = fromDatetime.AddDays(2); //2段时效，48小时预报
                while (fromDatetime < stopDatetime)
                {
                    DataRow newRow = AddNewRow(dt, tmpTable, forecastDate, fromDatetime, fromDatetime.AddDays(1), 7, r[0], timePeriod, Module);
                    tmpTable.Rows.Add(newRow);

                    fromDatetime = fromDatetime.AddDays(1);
                }

            }

            return tmpTable;
        }

        /// <summary>
        /// 根据Duration字典表，返回相应ID的时间区间
        /// </summary>
        /// <param name="dcDuration"></param>
        /// <param name="durationID"></param>
        /// <returns></returns>
        public string[] GetDurationSpan(DataTable dcDuration, int durationID)
        {
            string filter = string.Format("DM ={0}", durationID);
            DataRow[] rows = dcDuration.Select(filter);
            string mcValue = rows[0][1].ToString();
            return mcValue.Split('-');

        }

        /// <summary>
        /// 在表中插入一行，主要是计算分段为5,6,7的，对于平均值的计算需要考虑到分段的时间
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="tmpTable"></param>
        /// <param name="forecastDate"></param>
        /// <param name="fromDatetime"></param>
        /// <param name="toDatetime"></param>
        /// <param name="durationID"></param>
        /// <param name="itemID"></param>
        /// <param name="timePeriod">分段字典表</param>
        /// <returns></returns>
        private DataRow AddNewRow(DataTable dt, DataTable tmpTable, DateTime forecastDate, DateTime fromDatetime, DateTime toDatetime, int durationID, object itemID, DataTable timePeriod, string Module)
        {
            int period = 24;
            DataRow newRow = tmpTable.NewRow();
            string filter = string.Format("ITEMID = {0} AND LST >= '{1}' AND LST < '{2}' AND durationID<>6", itemID, fromDatetime, toDatetime);

            string value;
            newRow[0] = fromDatetime;
            newRow[1] = forecastDate;
            TimeSpan dateDiff = fromDatetime.Subtract(forecastDate);
            if (dateDiff.TotalHours >= 12)
                period = 48;
            newRow[2] = period;
            newRow[3] = durationID;
            newRow[4] = itemID;
            string str = itemID.ToString();
            if (str == "4" || str == "5")
            {
                value = dt.Compute("max(Value)", filter).ToString();
            }
            else
            {
                DataRow[] rows = dt.Select(filter);
                //value = dt.Compute("avg(Value)", filter).ToString();
                double sumValue = 0;
                int totalHours = 0;
                string[] durationSpan;//获取分段时间范围
                int span;

                for (int i = 0; i < rows.Length; i++)
                {
                    durationSpan = GetDurationSpan(timePeriod, int.Parse(rows[i][3].ToString()));//获取分段时间范围
                    span = int.Parse(durationSpan[1]) - int.Parse(durationSpan[0]);
                    if (span < 0)
                        span = 24 + span;//跨天
                    if (rows[i][5] != DBNull.Value)
                    {
                        sumValue = sumValue + double.Parse(rows[i][5].ToString()) * span;
                        totalHours = totalHours + span;
                    }
                }
                //计算平均值
                if (totalHours == 0)//当没有值的情况下
                    value = "";
                else
                    value = Convert.ToString((sumValue / totalHours));
            }

            if (value == "")
            {
                newRow[5] = DBNull.Value;
                newRow[6] = DBNull.Value;
            }
            else
            {
                newRow[5] = Math.Round(double.Parse(value), 1);
                newRow[6] = ToAQI(newRow[5].ToString(), itemID.ToString());
            }
            newRow[7] = 2;
            newRow[8] = Module;
            newRow[9] = DBNull.Value;


            return newRow;


        }
        //根据选择来发送短信
        public string PublicData(string checkBoxSelect, string content, string forecastDate, string phones, string phonesDX, string publishTime, string userName, string ForecastStyle)
        {
            DateTime forecastTime = DateTime.Parse(forecastDate).AddHours(18);
            try
            {
                string strSQL = @"UPDATE tb_AirForecast SET publishTime='" + publishTime + "' WHERE foreDate='" + forecastTime.ToString("yyyy年M月d日") + "'";
                m_Database.Execute(strSQL);
                StringBuilder returnString = new StringBuilder("{");
                string duanXin = "";
                string SHBJMess = "";
                string XJZXMess = "";
                string SSFBXT = "";
                string weiboMess = "";
                string TencentWeiBoStr;
                string DXMess = "";
                //移动短信
                if (checkBoxSelect.IndexOf("0") >= 0)
                {
                    duanXin = SendSM(content, forecastDate, phones, userName, "1", ForecastStyle);
                    returnString.Append(duanXin);
                    returnString.Append("，");
                }
                //市环保局
                if (checkBoxSelect.IndexOf("1") >= 0)
                {
                    SHBJMess = SHBJ(forecastDate, userName, "1", "", ForecastStyle);
                    returnString.Append(SHBJMess);
                    returnString.Append("，");
                }
                //宣教中心
                if (checkBoxSelect.IndexOf("2") >= 0)
                {
                    XJZXMess = XJZX(forecastDate, userName, "1", "", ForecastStyle);
                    returnString.Append(XJZXMess);
                    returnString.Append("，");
                }
                //实时发布系统
                if (checkBoxSelect.IndexOf("3") >= 0)
                {
                    SSFBXT = InsertDataBase(content, forecastDate, userName, "1", ForecastStyle);
                    returnString.Append(SSFBXT);
                    returnString.Append("，");
                }
                //新浪微博
                if (checkBoxSelect.IndexOf("4") >= 0)
                {
                    weiboMess = weiBo(content, forecastDate, userName, "1", ForecastStyle);
                    returnString.Append(weiboMess);
                    returnString.Append("，");
                }
                //电信
                if (checkBoxSelect.IndexOf("5") >= 0)
                {
                    DXMess = SendSMDX(content, forecastDate, phonesDX, userName, "1", ForecastStyle);
                    returnString.Append(DXMess);
                    returnString.Append("，");
                }
                //腾讯微博
                if (checkBoxSelect.IndexOf("6") >= 0)
                {
                    TencentWeiBoStr = TencentWeiBo(content, forecastDate, userName, "1", ForecastStyle);
                    returnString.Append(TencentWeiBoStr);
                    returnString.Append("，");
                }
                returnString.Remove(returnString.Length - 1, 1);
                returnString.Append("}");
                string sendMess = returnString.ToString();
                m_Log.Info("PublicData:" + sendMess);
                return sendMess;
            }
            catch (Exception ex)
            {
                m_Log.Error("PublicData", ex);
                return ex.Message;
            }
        }
        public string TencentWeiBo(string content, string forecastDate, string userName, string countNum, string ForecastStyle)
        {
            string strSQL;
            string sendInfo;
            string returnStrInfo = "";
            int count = int.Parse(countNum);
            try
            {
                SendWeiBo SendWeiBo = new SendWeiBo();

                //string[] conAry = content.Split('，');
                //string firstStr = conAry[1];
                //string zjStr = conAry[4];
                //int indexPos = content.IndexOf(firstStr);
                //int zjindexPos = content.IndexOf(zjStr);
                //if (conAry.Length > 7)
                //{
                //    string endStr = conAry[7];
                //    int endindexPos = content.IndexOf(endStr);
                //    content = conAry[0] + "，" + conAry[2] + "，" + conAry[3] + "，" + conAry[5] + "，" + conAry[6] + "，" + conAry[8] + "，" + conAry[9];
                //}
                //else
                //{
                //    content = conAry[0] + "，" + conAry[2] + "，" + conAry[3] + "，" + conAry[5] + "，" + conAry[6];
                //}

                string TencentReturn = SendWeiBo.SendTencent(content);
                if (TencentReturn == "成功")
                {
                    sendInfo = "发送成功";
                    returnStrInfo = "腾讯微博发送成功";
                }
                else
                {
                    sendInfo = "发送失败";
                    returnStrInfo = "腾讯微博发送失败，原因是" + TencentReturn;
                }
                if (count == 1)
                    strSQL = "INSERT INTO T_SendLog VALUES('" + DateTime.Now.ToString() + "','腾讯微博','" + ForecastStyle + "', '" + content + "', '" + userName + "', '" + sendInfo + "','1','','" + forecastDate + "')";
                else
                    strSQL = "UPDATE T_SendLog SET Recount='" + count + "',Message='" + sendInfo + "'  WHERE DateTime='" + forecastDate + "' AND PublicStyle='腾讯微博'";
                m_Database.Execute(strSQL);
                return returnStrInfo;
            }
            catch (Exception ex)
            {
                m_Log.Error("TencentWeiBo", ex);
                return ex.Message;
            }


        }
        public string weiBo(string content, string forecastDate, string userName, string countNum, string ForecastStyle)
        {
            string strSQL;
            string sendInfo;
            string returnStrInfo = "";
            int count = int.Parse(countNum);
            try
            {
                SendWeiBo SendWeiBo = new SendWeiBo();
                string sinaReturn = SendWeiBo.SendSina(content);

                if (sinaReturn == "成功")
                {
                    sendInfo = "发送成功";
                    returnStrInfo = "新浪微博发送成功";

                }
                else
                {
                    sendInfo = "发送失败";
                    returnStrInfo = "新浪微博发送失败，原因是" + sinaReturn;
                }
                if (count == 1)
                    strSQL = "INSERT INTO T_SendLog VALUES('" + DateTime.Now.ToString() + "','新浪微博','" + ForecastStyle + "', '" + content + "', '" + userName + "', '" + sendInfo + "','1','','" + forecastDate + "')";
                else
                    strSQL = "UPDATE T_SendLog SET Recount='" + count + "',Message='" + sendInfo + "'  WHERE DateTime='" + forecastDate + "' AND PublicStyle='新浪微博'";
                m_Database.Execute(strSQL);
                return returnStrInfo;
            }
            catch (Exception ex)
            {
                m_Log.Error("weiBo", ex);
                return ex.Message;
            }
        }
        //发到“市环保局”代码
        public string SHBJ(string forecastDate, string userName, string countNum, string content, string ForecastStyle)
        {
            //DateTime.Parse(DateTime.Parse(forecastDate).ToShortDateString()).AddHours(18);
            DateTime forecastTime = DateTime.Parse(DateTime.Parse(forecastDate).ToShortDateString()).AddHours(18);
            string strSQL;
            string sendInfo;
            string returnStrInfo = "";
            string sentContent;
            int count = int.Parse(countNum);
            try
            {
                if (content != "")
                {
                    strSQL = "SELECT * FROM tb_AirForecast WHERE foreDate='" + forecastTime.ToString("yyyy年M月d日") + "'";
                    DataTable db = m_Database.GetDataTable(strSQL);
                    string foreContent = "";
                    if (db.Rows.Count > 0)
                    {
                        for (int i = 0; i < db.Columns.Count - 1; i++)
                        {
                            foreContent = foreContent + "|" + db.Rows[0][i + 1].ToString();
                        }
                    }
                    sentContent = foreContent.Remove(0, 1);
                }
                else
                {
                    sentContent = content;

                }

                PushAQIOC poc = new PushAQIOC();
                poc.Url = @"http://219.233.250.182/AQIforSEIC/PushAQIOC.asmx";


                poc.UseDefaultCredentials = true;

                poc.PreAuthenticate = true;

                poc.Credentials = System.Net.CredentialCache.DefaultCredentials;
                ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["WebService"];
                string constring = settings.ConnectionString;
                string[] parts = constring.Split(new char[] { ';', '=' }, StringSplitOptions.None);
                try
                {
                    string result = "";
                    result = poc.importAQIForecast(sentContent, "|", parts[11]);
                    returnStrInfo = string.Format("市环保局发送{0}", result);
                    if (result == "成功")
                        sendInfo = "发送成功";
                    else
                        sendInfo = "发送" + result;

                }
                catch (Exception ex)
                {
                    sendInfo = "发送失败";
                    returnStrInfo = string.Format("市环保局发送失败,原因是{0}", ex.ToString());
                }
                if (count == 1)
                    strSQL = "INSERT INTO T_SendLog VALUES('" + DateTime.Now.ToString() + "','市环保局','" + ForecastStyle + "', '" + sentContent + "', '" + userName + "', '" + sendInfo + "','1','','" + forecastDate + "')";
                else
                    strSQL = "UPDATE T_SendLog SET Recount='" + count + "',Message='" + sendInfo + "'  WHERE DateTime='" + forecastDate + "' AND PublicStyle='市环保局'";
                m_Database.Execute(strSQL);
                return returnStrInfo;
            }
            catch (Exception ex)
            {
                m_Log.Error("SHBJ", ex);
                return ex.Message;
            }

        }
        //发到宣教中心
        public string XJZX(string forecastDate, string userName, string countNum, string content, string ForecastStyle)
        {
            DateTime forecastTime = DateTime.Parse(DateTime.Parse(forecastDate).ToShortDateString()).AddHours(18);
            string strSQL;
            string returnStrInfo = "";
            string sentContent;
            string foreContent = "";
            string sendInfo;
            int count = int.Parse(countNum);
            try
            {
                try
                {
                    if (content != "")
                    {
                        strSQL = "SELECT * FROM tb_AirForecast WHERE foreDate='" + forecastTime.ToString("yyyy年M月d日") + "'";
                        DataTable db = m_Database.GetDataTable(strSQL);
                        if (db.Rows.Count > 0)
                        {
                            for (int i = 0; i < db.Columns.Count - 1; i++)
                            {
                                foreContent = foreContent + "|" + db.Rows[0][i + 1].ToString();
                            }
                        }
                        sentContent = foreContent.Remove(0, 1);
                    }
                    else
                    {
                        sentContent = content;
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }

                ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["WebService"];
                string constring = settings.ConnectionString;
                string[] parts = constring.Split(new char[] { ';', '=' }, StringSplitOptions.None);
                Lucas.importAQI imAQI = new Lucas.importAQI();
                imAQI.Url = "http://www.envir.gov.cn/aqiforseec/importAQI.asmx";
                //WebProxy proxy = new WebProxy();
                //proxy = new WebProxy(parts[1], true);  //代理服务器信息可通过config文件配置            
                //proxy.Credentials = new NetworkCredential(parts[7], parts[9], parts[3]); //参数UserName, Password，Domain可通过config文件配置                      
                //imAQI.Proxy = proxy;
                try
                {
                    string resultString = "";
                    resultString = imAQI.importAQIForecast(sentContent, "|", parts[11]);
                    returnStrInfo = string.Format("宣教中心发送{0}", resultString);
                    sendInfo = "发送" + resultString;
                }
                catch (Exception ex)
                {
                    sendInfo = "发送失败";
                    returnStrInfo = string.Format("宣教中心发送失败,原因是{0}", ex.ToString());
                }
                if (count == 1)
                    strSQL = "INSERT INTO T_SendLog VALUES('" + DateTime.Now.ToString() + "','宣教中心','" + ForecastStyle + "', '" + sentContent + "', '" + userName + "', '" + sendInfo + "','1','','" + forecastDate + "')";
                else
                    strSQL = "UPDATE T_SendLog SET Recount='" + count + "',Message='" + sendInfo + "'  WHERE DateTime='" + forecastDate + "' AND PublicStyle='宣教中心'";

                try
                {
                    m_Database.Execute(strSQL);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                return returnStrInfo;
            }
            catch (Exception ex)
            {
                m_Log.Error("XJZX", ex);
                return ex.Message;
            }

        }
        public string InsertDataBase(string content, string forecastDate, string userName, string countNum, string ForecastStyle)
        {
            DateTime forecastTime = DateTime.Parse(forecastDate).AddHours(18);
            string sendInfo;
            string returnStrInfo = "";
            int count = int.Parse(countNum);
            string flag = "";
            if (ForecastStyle == "综合预报")
                flag = "Former";
            else
                flag = "Modify";
            try
            {
                string strSQL, existsSQL, updateSQL, insertSQL;
                try
                {
                    existsSQL = "SELECT ForecastDate FROM T_Forecast WHERE ForecastDate='" + forecastTime + "' AND Flag='" + flag + "'";
                    updateSQL = "UPDATE T_Forecast SET UpdateDate=GETDATE(), Message='" + content + "' WHERE ForecastDate='" + forecastTime + "' AND Flag='" + flag + "'";
                    insertSQL = "INSERT INTO T_Forecast(ForecastDate,Message,Flag,UpdateDate) VALUES('" + forecastTime + "', '" + content + "','" + flag + "',GetDate())";
                    m_Database.Execute(existsSQL, updateSQL, insertSQL);

                    try
                    {
                        strSQL = "SELECT * FROM tb_AirForecast WHERE foreDate='" + forecastTime.ToString("yyyy年M月d日") + "'";
                        DataTable db = m_Database.GetDataTable(strSQL);
                        db.TableName = "tb_AirForecast";
                        Database m_DatabaseNew = new Database("SEMCAIR");
                        strSQL = "TRUNCATE TABLE tb_AirForecast";
                        m_DatabaseNew.Execute(strSQL);//删除已有记录
                        strSQL = "TRUNCATE TABLE semc_air_1hr.DBO.tb_AirForecast";
                        m_DatabaseNew.Execute(strSQL);
                        bool returnStr = m_DatabaseNew.BulkCopy(db);
                        strSQL = "insert into semc_air_1hr.dbo.tb_AirForecast(foreDate,Seg1,AQI1,Grade1,Param1,Seg2,AQI2,Grade2,Param2,Seg3,AQI3,Grade3,Param3,Detail,Sign,publishTime) select dbo.ToNY(foreDate) as foreDate ,Seg1,AQI1,Grade1,Param1,Seg2,AQI2,Grade2,Param2,Seg3,AQI3,Grade3,Param3,Detail,Sign,publishTime from tb_AirForecast";
                        m_DatabaseNew.Execute(strSQL);
                        if (returnStr)
                        {
                            sendInfo = "发送成功";
                            returnStrInfo = "实时发布系统发送成功";
                        }
                        else
                        {
                            sendInfo = "发送失败";
                            returnStrInfo = "实时发布系统发送失败";
                        }

                    }
                    catch (Exception ex)
                    {
                        sendInfo = "发送失败";
                        returnStrInfo = "实时发布系统发送失败，" + ex.ToString();
                    }

                }
                catch (Exception ex)
                {
                    sendInfo = "发送失败";
                    returnStrInfo = string.Format("实时发布系统发送失败,原因是{0}", ex.ToString());

                }
                if (count == 1)
                    strSQL = "INSERT INTO T_SendLog VALUES('" + DateTime.Now.ToString() + "','实时发布系统','" + ForecastStyle + "', '" + content + "', '" + userName + "', '" + sendInfo + "','1','','" + forecastDate + "')";
                else
                    strSQL = "UPDATE T_SendLog SET Recount='" + count + "',Message='" + sendInfo + "'  WHERE DateTime='" + forecastDate + "' AND PublicStyle='实时发布系统'";
                m_Database.Execute(strSQL);
                return returnStrInfo;
            }
            catch (Exception ex)
            {
                m_Log.Error("InsertDataBase", ex);
                return ex.Message;
            }
        }

        /// <summary>
        /// 实现预报内容的短信发送，并返回发送结果，把发送的情况入库
        /// <param name="content">发送内容</param>
        /// <param name="forecastDate">预报日期</param>
        /// <param name="phones">电话号码</param>
        /// <param name="userName">发送人员</param>
        /// <param name="count">发送次数</param>
        /// <param name="ForecastStyle">预报类型</param>
        /// <returns>返回是否发送成功的结果</returns>
        public string SendSMDX(string content, string forecastDate, string phones, string userName, string countNum, string ForecastStyle)
        {

            string returnMsg = "";
            string strSQL = "";
            string sendInfo = "发送成功";
            string sendContent = "";
            string contentTemp = "";
            string messageReturn = "";
            string endContent = "";
            string messageReturnStr = "";
            string url = "http://10.200.254.21:9090/ucp/services/UCPPlatService";
            string[] loginParams = new string[2];
            loginParams[0] = "huanbaosend";
            loginParams[1] = "huanbaosend";
            object[] sendMessParams = new object[7];
            int count = int.Parse(countNum);
            try
            {
                string loginReturn = WebServiceHelper.InvokeWebService(url, "UCPPlatWebServiceService", "login", loginParams).ToString();
                if (loginReturn.IndexOf("ERROR") >= 0)
                {
                    returnMsg = "电信联通发送失败,原因是" + loginReturn;
                    sendInfo = "发送失败";
                }
                else
                {
                    sendMessParams[0] = loginReturn;
                    sendMessParams[1] = "NORM";
                    sendMessParams[2] = phones;
                    sendMessParams[3] = DateTime.Now;
                    sendMessParams[5] = "";
                    sendMessParams[6] = false;
                    //获取需要发送的手机号码
                    if (phones != "")
                    {
                        if (content.Length > 200)
                        {
                            contentTemp = content;
                            int countLength = 0;
                            if (content.Length % 190 == 0)
                                countLength = content.Length / 190;
                            else
                            {
                                countLength = content.Length / 190 + 1;
                                endContent = content.Substring(content.Length / 190 * 190);
                            }
                            for (int i = 1; i < content.Length / 190 + 1; i++)
                            {
                                sendContent = contentTemp.Substring(0, 190);
                                contentTemp = contentTemp.Substring(190);
                                sendMessParams[4] = countLength.ToString() + "/" + i.ToString() + " " + sendContent;
                                messageReturn = WebServiceHelper.InvokeWebService(url, "UCPPlatWebServiceService", "sendSMS", sendMessParams).ToString();
                                messageReturnStr = messageReturnStr + messageReturn;

                            }
                            if (endContent != "")
                            {
                                sendMessParams[4] = countLength.ToString() + "/" + countLength.ToString() + " " + endContent;
                                messageReturn = WebServiceHelper.InvokeWebService(url, "UCPPlatWebServiceService", "sendSMS", sendMessParams).ToString();
                                messageReturnStr = messageReturnStr + messageReturn;
                            }


                        }
                        else
                        {
                            sendMessParams[4] = content;
                            messageReturn = WebServiceHelper.InvokeWebService(url, "UCPPlatWebServiceService", "sendSMS", sendMessParams).ToString();
                            messageReturnStr = messageReturn;
                        }
                        if (messageReturnStr.IndexOf("ERROR") >= 0)
                        {
                            sendInfo = "发送失败";
                            returnMsg = "电信联通发送失败,原因是" + loginReturn;
                        }
                        else
                        {
                            sendInfo = "发送成功";
                            returnMsg = "电信联通发送成功！";
                        }

                    }
                    else
                    {
                        returnMsg = "电信联通失败，没有要发送的手机号码";
                        sendInfo = "发送失败";
                    }
                }
                string[] loginOff = new string[1];
                loginOff[0] = loginReturn;
                WebServiceHelper.InvokeWebService(url, "UCPPlatWebServiceService", "logoff", loginOff);
                if (count == 1)
                    strSQL = "INSERT INTO T_SendLog VALUES('" + DateTime.Now.ToString() + "','电信联通','" + ForecastStyle + "', '" + content + "', '" + userName + "', '" + sendInfo + "','1','" + phones + "','" + forecastDate + "')";
                else
                    strSQL = "UPDATE T_SendLog SET Recount='" + count + "',Message='" + sendInfo + "'  WHERE DateTime='" + forecastDate + "' AND PublicStyle='电信联通'";
                m_Database.Execute(strSQL);
                m_Log.Info("SendSM:" + returnMsg);
                return returnMsg;
            }
            catch (Exception ex)
            {
                m_Log.Error("SendSMDX", ex);
                return ex.Message;
            }
        }





        //public string SendSM(string content, string forecastDate, string phones, string userName, string countNum, string ForecastStyle)
        //  {
        //      string returnMsg = "";
        //      string strSQL = "";
        //      string sendInfo = "发送成功";
        //      string sendContent = "";
        //      string contentTemp = "";
        //      string messageReturn = "";
        //      string endContent = "";
        //      string messageReturnStr = "";
        //      string url = "http://10.200.254.21:9090/ucp/services/UCPPlatService";
        //      string[] loginParams = new string[2];
        //      loginParams[0] = "huanbaosend";
        //      loginParams[1] = "huanbaosend";
        //      object[] sendMessParams = new object[7];
        //      int count = int.Parse(countNum);
        //      try
        //      {
        //          string loginReturn = WebServiceHelper.InvokeWebService(url, "UCPPlatWebServiceService", "login", loginParams).ToString();
        //          if (loginReturn.IndexOf("ERROR") >= 0)
        //          {
        //              returnMsg = "发送移动短信失败,原因是" + loginReturn;
        //              sendInfo = "发送失败";
        //          }
        //          else
        //          {
        //              sendMessParams[0] = loginReturn;
        //              sendMessParams[1] = "NORM";
        //              sendMessParams[2] = phones;
        //              sendMessParams[3] = DateTime.Now;
        //              sendMessParams[5] = "";
        //              sendMessParams[6] = false;
        //              //获取需要发送的手机号码
        //              if (phones != "")
        //              {
        //                  if (content.Length > 200)
        //                  {
        //                      contentTemp = content;
        //                      int countLength = 0;
        //                      if (content.Length % 190 == 0)
        //                          countLength = content.Length / 190;
        //                      else
        //                      {
        //                          countLength = content.Length / 190 + 1;
        //                          endContent = content.Substring(content.Length / 190 * 190);
        //                      }
        //                      for (int i = 1; i < content.Length / 190 + 1; i++)
        //                      {
        //                          sendContent = contentTemp.Substring(0, 190);
        //                          contentTemp = contentTemp.Substring(190);
        //                          sendMessParams[4] = countLength.ToString() + "/" + i.ToString() + " " + sendContent;
        //                          messageReturn = WebServiceHelper.InvokeWebService(url, "UCPPlatWebServiceService", "sendSMS", sendMessParams).ToString();
        //                          messageReturnStr = messageReturnStr + messageReturn;

        //                      }
        //                      if (endContent != "")
        //                      {
        //                          sendMessParams[4] = countLength.ToString() + "/" + countLength.ToString() + " " + endContent;
        //                          messageReturn = WebServiceHelper.InvokeWebService(url, "UCPPlatWebServiceService", "sendSMS", sendMessParams).ToString();
        //                          messageReturnStr = messageReturnStr + messageReturn;
        //                      }


        //                  }
        //                  else
        //                  {
        //                      sendMessParams[4] = content;
        //                      messageReturn = WebServiceHelper.InvokeWebService(url, "UCPPlatWebServiceService", "sendSMS", sendMessParams).ToString();
        //                      messageReturnStr = messageReturn;
        //                  }
        //                  if (messageReturnStr.IndexOf("ERROR") >= 0)
        //                  {
        //                      sendInfo = "发送失败";
        //                      returnMsg = "发送移动短信失败,原因是" + loginReturn;
        //                  }
        //                  else
        //                  {
        //                      sendInfo = "发送成功";
        //                      returnMsg = "发送移动短信成功";
        //                  }

        //              }
        //              else
        //              {
        //                  returnMsg = "移动失败，没有要发送的手机号码";
        //                  sendInfo = "发送失败";
        //              }
        //          }
        //          string[] loginOff = new string[1];
        //          loginOff[0] = loginReturn;
        //          WebServiceHelper.InvokeWebService(url, "UCPPlatWebServiceService", "logoff", loginOff);
        //          if (count == 1)
        //              strSQL = "INSERT INTO T_SendLog VALUES('" + DateTime.Now.ToString() + "','移动短信','" + ForecastStyle + "', '" + content + "', '" + userName + "', '" + sendInfo + "','1','" + phones + "','" + forecastDate + "')";
        //          else
        //              strSQL = "UPDATE T_SendLog SET Recount='" + count + "',Message='" + sendInfo + "'  WHERE DateTime='" + forecastDate + "' AND PublicStyle='移动短信'";
        //          m_Database.Execute(strSQL);
        //          m_Log.Info("SendSM:" + returnMsg);
        //          return returnMsg;
        //      }
        //      catch (Exception ex)
        //      {
        //          m_Log.Error("SendSMDX", ex);
        //          return ex.Message;
        //      }

        //  }

        /// <summary>
        /// 实现预报内容的短信发送，并返回发送结果，把发送的情况入库
        /// <param name="content">发送内容</param>
        /// <param name="forecastDate">预报日期</param>
        /// <param name="phones">电话号码</param>
        /// <param name="userName">发送人员</param>
        /// <param name="count">发送次数</param>
        /// <param name="ForecastStyle">预报类型</param>
        /// <returns>返回是否发送成功的结果</returns>
        public string SendSM(string content, string forecastDate, string phones, string userName, string countNum, string ForecastStyle)
        {
            try
            {
                MasSender masSender = new MasSender();
                string returnMsg = string.Empty;
                string strSQL = "";
                int j = 0;
                int count = int.Parse(countNum);
                //获取需要发送的手机号码
                if (phones != "")
                {
                    string[] mobiles = phones.Split(',');
                    string phonesJoin = string.Join(",", mobiles);
                    //发送短信
                    int ret = masSender.SendSM(mobiles, content);
                    masSender.Relese();
                    string sendInfo;
                    if (ret == 0)
                        sendInfo = "发送成功";
                    else
                        sendInfo = "发送失败";
                    if (count == 1)
                        strSQL = "INSERT INTO T_SendLog VALUES('" + DateTime.Now.ToString() + "','移动短信','" + ForecastStyle + "', '" + content + "', '" + userName + "', '" + sendInfo + "','1','" + phonesJoin + "','" + forecastDate + "')";
                    else
                        strSQL = "UPDATE T_SendLog SET Recount='" + count + "',Message='" + sendInfo + "'  WHERE DateTime='" + forecastDate + "' AND PublicStyle='移动短信'";
                    m_Database.Execute(strSQL);
                    switch (ret)
                    {
                        case 0:
                            returnMsg = string.Format("发送移动短信成功");
                            break;
                        case -1:
                            returnMsg = string.Format("发送移动短信失败，原因是连接数据库出错,编号为{0}", ret);
                            break;
                        case -2:
                            returnMsg = string.Format("发送移动短信失败，原因是数据库关闭失败,编号为{0}", ret);
                            break;
                        case -3:
                            returnMsg = string.Format("发送移动短信失败，原因是数据库插入错误,编号为{0}", ret);
                            break;
                        case -4:
                            returnMsg = string.Format("发送移动短信失败，原因是数据库删除错误,编号为{0}", ret);
                            break;
                        case -5:
                            returnMsg = string.Format("发送移动短信失败，原因是数据库查询错误，编号为{0}", ret);
                            break;
                        case -6:
                            returnMsg = string.Format("发送移动短信失败，原因是数据库参数错误，编号为{0}", ret);
                            break;
                        case -7:
                            returnMsg = string.Format("发送移动短信失败，原因是API编码非法，编号为{0}", ret);
                            break;
                        case -8:
                            returnMsg = string.Format("发送移动短信失败，原因是参数超长，编号为{0}", ret);
                            break;
                        case -9:
                            returnMsg = string.Format("发送移动短信失败，原因是没有初始化或初始化失败，编号为{0}", ret);
                            break;
                        case -10:
                            returnMsg = string.Format("发送移动短信失败，原因是API接口处于暂停（失效）状态，编号为{0}", ret);
                            break;
                        case -11:
                            returnMsg = string.Format("发送移动短信失败，原因是短信网关未连接，编号为{0}", ret);
                            break;
                        case 1:
                            returnMsg = string.Format("发送移动短信失败，原因是发送内容为空，编号为{0}", ret);
                            break;
                        case 2:
                            returnMsg = string.Format("发送移动短信失败，原因是发送内容中存在被禁止词组，编号为{0}", ret);
                            break;
                        case 3:
                            returnMsg = string.Format("发送移动短信失败，原因是手机号码不正确，编号为{0}", ret);
                            break;
                        case 4:
                            returnMsg = string.Format("发送移动短信失败，原因是手机号码为运营商所禁止，编号为{0}", ret);
                            break;
                        case 5:
                            returnMsg = string.Format("发送移动短信失败，原因是手机号码存在黑名单中，编号为{0}", ret);
                            break;
                        case 6:
                            returnMsg = string.Format("发送移动短信失败，原因是手机号码不存在白名单中，编号为{0}", ret);
                            break;
                        case 7:
                            returnMsg = string.Format("发送移动短信失败，原因是企业欠费，编号为{0}", ret);
                            break;
                        case 8:
                            returnMsg = string.Format("发送移动短信失败，原因是通讯异常，编号为{0}", ret);
                            break;
                        case 101:
                            returnMsg = string.Format("发送移动短信失败，原因是系统错误，编号为{0}", ret);
                            break;
                        case 102:
                            returnMsg = string.Format("发送移动短信失败，原因是短信内容无法到达手机，编号为{0}", ret);
                            break;
                        default:
                            returnMsg = string.Format("发送移动短信失败，编号为{0}", ret);
                            break;
                    }
                }
                else
                    returnMsg = "发送移动短信失败，没有要发送的手机号码";
                m_Log.Info("SendSM:" + returnMsg);
                return returnMsg;
            }
            catch (Exception ex)
            {
                m_Log.Error("SendSM", ex);
                return ex.Message;
            }
        }


        public string changeEdits(string forecastDate, string module)
        {
            string forecastTime = DateTime.Parse(forecastDate).ToString("yyyy年M月d日");
            int hour = int.Parse(DateTime.Now.Hour.ToString());
            string strSQL = "SELECT * FROM tb_AirForecast WHERE foreDate='" + forecastTime + "'";
            DataTable dt = m_Database.GetDataTable(strSQL);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<table id='changeDatable'  width='100%' border='0' cellpadding='0' cellspacing='0'>");
            sb.AppendLine("<tr>");

            //创建抬头
            sb.AppendLine("<td class='tableEditorRight'>时段</td>");
            sb.AppendLine("<td class='tableEditor'>空气质量</td>");
            sb.AppendLine("<td class='tableEditor'>首要污染物</td>");
            sb.AppendLine("<td class='tableEditor'>AQI</td>");
            sb.AppendLine("</tr>");
            if (dt.Rows.Count > 0)
            {
                if (module == "Modify" && hour <= 15)
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine(string.Format("<td class='tableRowEditorRight' style='line-height: 27px;display:none'><div contenteditable='true' class='divInputTypeNew' id='{1}' >{0}</div>", dt.Rows[0]["Seg1"], "Seg1"));
                    sb.AppendLine(string.Format("<td class='tableRowEditor'  style='line-height: 27px;display:none'><div contenteditable='true' id='{1}' class='divInputTypeNew'>{0}</div>", dt.Rows[0]["Grade1"], "Grade1"));
                    sb.AppendLine(string.Format("<td class='tableRowEditor' style='display:none'><div  id='{1}' class='divInputTypeNew'>{0}</div>", dt.Rows[0]["Param1"], "Param1"));
                    sb.AppendLine(string.Format("<td class='tableRowEditor' style='line-height: 27px ;display:none'><div contenteditable='true' id='{1}' class='divInputTypeNew'>{0}</div>", dt.Rows[0]["AQI1"], "AQI1"));
                    sb.AppendLine("</tr>");
                    for (int i = 2; i < 4; i++)
                    {
                        sb.AppendLine("<tr>");
                        sb.AppendLine(string.Format("<td class='tableRowEditorRight' style='line-height: 27px'><div contenteditable='true' class='divInputTypeNew' id='{1}' >{0}</div>", dt.Rows[0]["Seg" + i.ToString()], "Seg" + i.ToString()));
                        sb.AppendLine(string.Format("<td class='tableRowEditor'  style='line-height: 27px'><div contenteditable='true' id='{1}' class='divInputTypeNew'>{0}</div>", dt.Rows[0]["Grade" + i.ToString()], "Grade" + i.ToString()));
                        sb.AppendLine(string.Format("<td class='tableRowEditor'><div  id='{1}' class='divInputTypeNew'>{0}</div>", dt.Rows[0]["Param" + i.ToString()], "Param" + i.ToString()));
                        sb.AppendLine(string.Format("<td class='tableRowEditor' style='line-height: 27px'><div contenteditable='true' id='{1}' class='divInputTypeNew'>{0}</div>", dt.Rows[0]["AQI" + i.ToString()], "AQI" + i.ToString()));
                        sb.AppendLine("</tr>");
                    }
                }
                else
                {
                    for (int i = 1; i < 4; i++)
                    {
                        sb.AppendLine("<tr>");
                        sb.AppendLine(string.Format("<td class='tableRowEditorRight' style='line-height: 27px'><div contenteditable='true'  class='divInputTypeNew' id='{1}' >{0}</div>", dt.Rows[0]["Seg" + i.ToString()], "Seg" + i.ToString()));
                        sb.AppendLine(string.Format("<td class='tableRowEditor'  style='line-height: 27px'><div contenteditable='true' id='{1}' class='divInputTypeNew'>{0}</div>", dt.Rows[0]["Grade" + i.ToString()], "Grade" + i.ToString()));
                        sb.AppendLine(string.Format("<td class='tableRowEditor'><div  id='{1}' class='divInputTypeNew'>{0}</div>", dt.Rows[0]["Param" + i.ToString()], "Param" + i.ToString()));
                        sb.AppendLine(string.Format("<td class='tableRowEditor' style='line-height: 27px'><div contenteditable='true' id='{1}' class='divInputTypeNew'>{0}</div>", dt.Rows[0]["AQI" + i.ToString()], "AQI" + i.ToString()));
                        sb.AppendLine("</tr>");
                    }
                }
            }
            sb.AppendLine("</table>");
            sb.AppendLine(string.Format("<label id='lableEditor' class='lableStyle'>发布描述：</label><div class='tableAirSign divInputTypeNew' id='Detail' contenteditable='true'>{0}</div>", dt.Rows[0]["Detail"]));
            sb.AppendLine(string.Format("<div class='tableAirSign' id='Sign' contenteditable='true'>{0}</div>", dt.Rows[0]["Sign"]));
            sb.AppendLine(string.Format("<div class='tableAirSign' id='publishTimeChange' contenteditable='true'>{0}</div>", dt.Rows[0]["publishTime"]));
            return sb.ToString();

        }
        public string ReEditsSave(string SegValueStr, string AQIValueStr, string GradeValueStr, string ParamValueStr, string SignValue, string PublicTime, string Detail, string forecastDate)
        {
            if (ParamValueStr.IndexOf("SUB") > 0)
                ParamValueStr = repalceStr(ParamValueStr, "SUB", "sub");
            string[] SegValue = SegValueStr.Split('|');
            string[] AQIValue = AQIValueStr.Split('|');
            string[] GradeValue = GradeValueStr.Split('|');
            string[] ParamValue = ParamValueStr.Split('|');
            if (Detail == "<br>")
                Detail = "";
            DateTime dtForecastDate = DateTime.Parse(forecastDate);
            string existsSQL, updateSQL, insertSQL;
            existsSQL = @"SELECT foreDate FROM tb_AirForecast WHERE foreDate='" + dtForecastDate.ToString("yyyy年M月d日") + "'";
            updateSQL = @"UPDATE tb_AirForecast SET Seg1='" + SegValue[0] + "',AQI1='" + AQIValue[0] + "',Grade1='" + GradeValue[0] + "',Param1='" + ParamValue[0] + "',Seg2='" + SegValue[1] + "',AQI2='" + AQIValue[1] + "',Grade2='" + GradeValue[1] + "',Param2='" + ParamValue[1] + "',Seg3='" + SegValue[2] + "',AQI3='" + AQIValue[2] + "',Grade3='" + GradeValue[2] + "',Param3='" + ParamValue[2] + "',Detail='" + Detail + "',Sign='" + SignValue + "',publishTime='" + PublicTime + "' WHERE foreDate='" + dtForecastDate.ToString("yyyy年M月d日") + "'";
            insertSQL = @"INSERT INTO tb_AirForecast VALUES('" + dtForecastDate.ToString("yyyy年M月d日") + "', '" + SegValue[0] + "','" + AQIValue[0] + "','" + GradeValue[0] + "','" + ParamValue[0] + "','" + SegValue[1] + "','" + AQIValue[1] + "','" + GradeValue[1] + "','" + ParamValue[1] + "','" + SegValue[2] + "','" + AQIValue[2] + "','" + GradeValue[2] + "','" + ParamValue[2] + "','" + Detail + "','" + SignValue + "','" + PublicTime + "')";
            try
            {
                int count = m_Database.Execute(existsSQL, updateSQL, insertSQL);
                if (count > 0)
                    return "更新成功";
                else
                    return "更新失败";
            }
            catch (Exception ex)
            {
                m_Log.Error("ReEditsSave", ex);
                return ex.ToString();
            }


        }
        public string repalceStr(string strString, string oldStr, string newStr)
        {
            while (strString.IndexOf(oldStr) > 0)
                strString = strString.Replace(oldStr, newStr);
            return strString;
        }
        public string getContentEdits(string forecastDate)
        {
            string date = DateTime.Parse(forecastDate).ToString("yyyy/M/d 18:00:00");
            string strSQL = "SELECT Message FROM T_Forecast WHERE ForecastDate='" + date + "'";
            string content;
            DataTable dt = m_Database.GetDataTable(strSQL);
            if (dt.Rows.Count > 0)
                content = dt.Rows[0][0].ToString();
            else
                content = "";
            return content;

        }
        public string getPublicState(string publicStyle)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<table id='PublicStateDatable'  width='100%' border='0' cellpadding='0' cellspacing='0' style='table-layout: fixed'>");
            sb.AppendLine("<tr>");

            //创建抬头
            sb.AppendLine("<td class='tableStateLeft'>序号</td>");
            sb.AppendLine("<td class='tableStateMiddle' >发布渠道</td>");
            sb.AppendLine("<td class='tableState'>状态</td>");
            sb.AppendLine("<td class='tableState'>重新发送</td>");
            sb.AppendLine("<td class='tableStateRight'>描述</td>");
            sb.AppendLine("</tr>");
            int index = 1;
            if (publicStyle.IndexOf('0') >= 0)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine(string.Format("<td class='tableStateLeft'>{0}</td>", index++));
                sb.AppendLine(string.Format("<td class='tableStateMiddle'>移动用户</td>"));
                sb.AppendLine(string.Format("<td class='tableState'><img id='ydUserImg'  src='images/wait.gif'/></td>"));
                sb.AppendLine(string.Format("<td class='tableState'><a href=\"javascript:reSentMobile();\"><img src='images/send.gif'/></a></td>"));
                sb.AppendLine(string.Format("<td class='tableStateRight' id='returnYDMessage'>正在发送</td>"));
                sb.AppendLine("</tr>");
            }
            if (publicStyle.IndexOf('1') >= 0)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine(string.Format("<td class='tableStateLeft'>{0}</td>", index++));
                sb.AppendLine(string.Format("<td class='tableStateMiddle'>市环保局</td>"));
                sb.AppendLine(string.Format("<td class='tableState'><img  id='shbjImg' src='images/hc.png'/></td>"));
                sb.AppendLine(string.Format("<td class='tableState'><a href=\"javascript:reSHBJ();\"><img src='images/send.gif'/></a></td>"));
                sb.AppendLine(string.Format("<td class='tableStateRight' id='returnSHBJMessage'>正在发送</td>"));
                sb.AppendLine("</tr>");
            }
            if (publicStyle.IndexOf('2') >= 0)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine(string.Format("<td class='tableStateLeft'>{0}</td>", index++));
                sb.AppendLine(string.Format("<td class='tableStateMiddle'>宣教中心</td>"));
                sb.AppendLine(string.Format("<td class='tableState'><img id='xjzxImg' src='images/hc.png'/></td>"));
                sb.AppendLine(string.Format("<td class='tableState'><a href=\"javascript:reXJZX();\"><img src='images/send.gif'/></a></td>"));
                sb.AppendLine(string.Format("<td class='tableStateRight' id='returnXJZXMessage'>正在发送</td>"));
                sb.AppendLine("</tr>");
            }
            if (publicStyle.IndexOf('3') >= 0)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine(string.Format("<td class='tableStateLeft'>{0}</td>", index++));
                sb.AppendLine(string.Format("<td class='tableStateMiddle'>实时发布系统</td>"));
                sb.AppendLine(string.Format("<td class='tableState'><img  id='ssfbImg' src='images/hc.png'/></td>"));
                sb.AppendLine(string.Format("<td class='tableState'><a href=\"javascript:reSSFB();\"><img src='images/send.gif'/></a></td>"));
                sb.AppendLine(string.Format("<td class='tableStateRight' id='returnSSFBMessage'>正在发送</td>"));
                sb.AppendLine("</tr>");
            }
            if (publicStyle.IndexOf('4') >= 0)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine(string.Format("<td class='tableStateLeft'>{0}</td>", index++));
                sb.AppendLine(string.Format("<td class='tableStateMiddle'>新浪微博</td>"));
                sb.AppendLine(string.Format("<td class='tableState'><img id='xlwbImg' src='images/hc.png'/></td>"));
                sb.AppendLine(string.Format("<td class='tableState'><a href=\"javascript:reXLWB();\"><img src='images/send.gif'/></a></td>"));
                sb.AppendLine(string.Format("<td class='tableStateRight' id='returnXLWBMessage'>正在发送</td>"));
                sb.AppendLine("</tr>");
            }
            if (publicStyle.IndexOf('5') >= 0)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine(string.Format("<td class='tableStateLeft'>{0}</td>", index++));
                sb.AppendLine(string.Format("<td class='tableStateMiddle'>联通电信</td>"));
                sb.AppendLine(string.Format("<td class='tableState'><img  id='dtdxImg' src='images/hc.png'/></td>"));
                sb.AppendLine(string.Format("<td class='tableState'><a href=\"javascript:reLTDX();\"><img src='images/send.gif'/></a></td>"));
                sb.AppendLine(string.Format("<td class='tableStateRight' id='returnLTDXMessage'>正在发送</td>"));
                sb.AppendLine("</tr>");
            }
            if (publicStyle.IndexOf('6') >= 0)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine(string.Format("<td class='tableStateLeft'>{0}</td>", index++));
                sb.AppendLine(string.Format("<td class='tableStateMiddle'>腾讯微博</td>"));
                sb.AppendLine(string.Format("<td class='tableState'><img id='txwbImg' src='images/hc.png'/></td>"));
                sb.AppendLine(string.Format("<td class='tableState'><a href=\"javascript:reTXWB();\"><img src='images/send.gif'/></a></td>"));
                sb.AppendLine(string.Format("<td class='tableStateRight' id='returnTXWBMessage'>正在发送</td>"));
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
            return sb.ToString();
        }

        public string tableStringII(DataSet temp, int count)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format("<table id='{0}' width='100%' border='0' cellpadding='0' cellspacing='0' class='tablekuang'>", temp.Tables[0].TableName));
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine("<th class='tabletitle' rowspan='2'>日期</th>");
            sb.AppendLine("<th class='tabletitle' rowspan='2'>时段名称</th>");
            sb.AppendLine("<th class='tabletitle' rowspan='2'>时段区间</th>");
            sb.AppendLine("<th class='tabletitle' rowspan='2'>预报时效</th>");

            sb.AppendLine("<th class='tabletitle' rowspan='2'>发布用户</th>");
            sb.AppendLine("<th class='tabletitle' rowspan='2'>分段得分</th>");
            sb.AppendLine("<th class='tabletitle' rowspan='2'>总计得分</th>");

            sb.AppendLine("<th class='tabletitle' colspan='4'>PM2.5浓度</th>");
            sb.AppendLine("<th class='tabletitle' colspan='4'>PM2.5AQI</th>");
            sb.AppendLine("<th class='tabletitle' colspan='4'>PM10浓度</th>");
            sb.AppendLine("<th class='tabletitle' colspan='4'>PM10AQI</th>");
            sb.AppendLine("<th class='tabletitle' colspan='4'>NO2浓度</th>");
            sb.AppendLine("<th class='tabletitle' colspan='4'>NO2AQI</th>");
            sb.AppendLine("<th class='tabletitle' colspan='4'>03-1h浓度</th>");
            sb.AppendLine("<th class='tabletitle' colspan='4'>03-1hAQI</th>");
            sb.AppendLine("<th class='tabletitle' colspan='4'>03-8h浓度</th>");
            sb.AppendLine("<th class='tabletitle' colspan='4'>03-8hAQI</th>");
            sb.AppendLine("<th class='tabletitle' colspan='2'>AQI实测</th>");
            sb.AppendLine("<th class='tabletitle' colspan='2'>AQI综合预报</th>");
            sb.AppendLine("<th class='tabletitle' colspan='2'>AQI(CMAQ)</th>");
            sb.AppendLine("<th class='tabletitle' colspan='2'>AQI(WRF-CHEM)</th>");

            sb.AppendLine("</tr>");

            #region
            sb.AppendLine("<tr>");
            sb.AppendLine("<th class='tabletitle'>实测</th>");
            sb.AppendLine("<th class='tabletitle'>综合预报</th>");
            sb.AppendLine("<th class='tabletitle'>CMAQ</th>");
            sb.AppendLine("<th class='tabletitle'>WRF-CHEM</th>");

            sb.AppendLine("<th class='tabletitle'>实测</th>");
            sb.AppendLine("<th class='tabletitle'>综合预报</th>");
            sb.AppendLine("<th class='tabletitle'>CMAQ</th>");
            sb.AppendLine("<th class='tabletitle'>WRF-CHEM</th>");

            sb.AppendLine("<th class='tabletitle'>实测</th>");
            sb.AppendLine("<th class='tabletitle'>综合预报</th>");
            sb.AppendLine("<th class='tabletitle'>CMAQ</th>");
            sb.AppendLine("<th class='tabletitle'>WRF-CHEM</th>");

            sb.AppendLine("<th class='tabletitle'>实测</th>");
            sb.AppendLine("<th class='tabletitle'>综合预报</th>");
            sb.AppendLine("<th class='tabletitle'>CMAQ</th>");
            sb.AppendLine("<th class='tabletitle'>WRF-CHEM</th>");

            sb.AppendLine("<th class='tabletitle'>实测</th>");
            sb.AppendLine("<th class='tabletitle'>综合预报</th>");
            sb.AppendLine("<th class='tabletitle'>CMAQ</th>");
            sb.AppendLine("<th class='tabletitle'>WRF-CHEM</th>");

            sb.AppendLine("<th class='tabletitle'>实测</th>");
            sb.AppendLine("<th class='tabletitle'>综合预报</th>");
            sb.AppendLine("<th class='tabletitle'>CMAQ</th>");
            sb.AppendLine("<th class='tabletitle'>WRF-CHEM</th>");

            sb.AppendLine("<th class='tabletitle'>实测</th>");
            sb.AppendLine("<th class='tabletitle'>综合预报</th>");
            sb.AppendLine("<th class='tabletitle'>CMAQ</th>");
            sb.AppendLine("<th class='tabletitle'>WRF-CHEM</th>");

            sb.AppendLine("<th class='tabletitle'>实测</th>");
            sb.AppendLine("<th class='tabletitle'>综合预报</th>");
            sb.AppendLine("<th class='tabletitle'>CMAQ</th>");
            sb.AppendLine("<th class='tabletitle'>WRF-CHEM</th>");

            sb.AppendLine("<th class='tabletitle'>实测</th>");
            sb.AppendLine("<th class='tabletitle'>综合预报</th>");
            sb.AppendLine("<th class='tabletitle'>CMAQ</th>");
            sb.AppendLine("<th class='tabletitle'>WRF-CHEM</th>");

            sb.AppendLine("<th class='tabletitle'>实测</th>");
            sb.AppendLine("<th class='tabletitle'>综合预报</th>");
            sb.AppendLine("<th class='tabletitle'>CMAQ</th>");
            sb.AppendLine("<th class='tabletitle'>WRF-CHEM</th>");

            sb.AppendLine("<th class='tabletitle'>AQI</th>");
            sb.AppendLine("<th class='tabletitle'>首要污染物</th>");

            sb.AppendLine("<th class='tabletitle'>AQI</th>");
            sb.AppendLine("<th class='tabletitle'>首要污染物</th>");

            sb.AppendLine("<th class='tabletitle'>AQI</th>");
            sb.AppendLine("<th class='tabletitle'>首要污染物</th>");

            sb.AppendLine("<th class='tabletitle'>AQI</th>");
            sb.AppendLine("<th class='tabletitle'>首要污染物</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            #endregion
            sb.AppendLine("<tbody>");
            sb.AppendLine("</tbody>");

            sb.Append("</table>");



            #region 赋值
            //int m = 0;
            //int k = 0;
            //    AQIExtention aqiExt;
            //    for (int i = 0; i < temp.Tables.Count; i++)
            //    { 
            //        DataTable dt = temp.Tables[i];
            //    foreach (DataRow dr in dt.Rows)
            //    {

            //        k++;
            //        string tableName = dt.TableName.ToString();
            //        int items = int.Parse(tableName.Substring(5, 1));
            //        sb.AppendLine(string.Format("<tr  onmouseover='mouseOver(this)' onmouseout='mouseOut(this)' id='{0}'>", tableName + k.ToString()));
            //        for (int j = 0; j < dt.Columns.Count; j++)
            //        {
            //            if (j == 0)
            //            {
            //                if (m == 0 || m % count == 0)
            //                    sb.AppendLine(string.Format("<td class='tablerow'  rowspan='{1}' id='{2}{3}{4}'>{0}</td>", dr[j].ToString(), count, tableName, k, j));
            //            }
            //            else
            //            {
            //                if ((items == 0 && (j == 4 || j == 6 || j == 8 || j == 10)) || ((j == 9 || j == 8 || j == 10 || j == 11) && items != 0))
            //                {
            //                    if (dr[j].ToString() != "")
            //                    {
            //                        aqiExt = new AQIExtention(int.Parse(dr[j].ToString()), items);
            //                        string aqiColor = string.Format("class='{0}'", aqiExt.Color);
            //                        sb.AppendLine(string.Format("<td class='tablerow' id='{2}{3}{4}'><span {0}>{1}</span></td>", aqiColor, int.Parse(dr[j].ToString()), tableName, k, j));

            //                    }
            //                    else
            //                    {
            //                        string value = dr[j].ToString() == "" ? "/ " : dr[j].ToString();
            //                        sb.AppendLine(string.Format("<td class='tablerow' id='{1}{2}{3}'>{0}</td>", value, tableName, k, j));
            //                    }

            //                }
            //                else
            //                {
            //                    string value = dr[j].ToString() == "" ? "/ " : dr[j].ToString();
            //                    sb.AppendLine(string.Format("<td class='tablerow' id='{1}{2}{3}'>{0}</td>", value, tableName, k, j));
            //                }

            //            }

            //        }
            //        sb.AppendLine("</tr>");
            //        m++;

            //    }
            //    }
            //    sb.AppendLine("</table>|");
            //}

            #endregion
            string json = sb.ToString();//ProcessJson(sb.ToString());
            //int posi = json.LastIndexOf("|");
            //string returnJson = json.Substring(0, posi);
            return json;
        }

        private string ProcessJson(string xml)
        {
            string filter = "</table>|\r\n<table id='table{0}' width='100%' border='0' cellpadding='0' cellspacing='0' class='tablekuang'>";
            xml = xml.Replace(string.Format(filter, "2"), "");
            xml = xml.Replace(string.Format(filter, "3"), "");
            xml = xml.Replace(string.Format(filter, "4"), "");
            xml = xml.Replace(string.Format(filter, "5"), "");
            filter = "<table id='table{0}'  width='100%' border='0' cellpadding='0' cellspacing='0' class='tablekuang'>";
            xml = xml.Replace(string.Format(filter, "0"), "");
            xml = xml.Replace("|", "").Replace("</table>", "");
            xml = xml + "\r\n</table>";
            xml = string.Format(xml, "style=\"visiblity:hidden;\"");
            return xml;
        }

        //2015年11月11日，闫海涛，从外部Excel读取填充页面
        public string ReadAQIFromExcel()
        {
            try
            {
                string filePath = @"E:\浦东项目\20151109\气象局会议\产品模板\产品模板\环境预报制作与分发\AQI分时段预报\AQI分时段预报制作表格.xlsx";
                Workbook workbook = new Workbook(filePath);
                //workbook.Open(filePath);
                string strJson = null;
                Cells cells = workbook.Worksheets[0].Cells;
                //表示单元格类型（P:监测中心或气象局，H:综合数据）
                string strCellType = "P";
                //表示单元格机构类型（1:监测中心，2:气象局）
                string strCellOrgType = "1";
                //表示单元格日期类型（3：当天，4：明天，5：后天）
                string strCellDateType = "3";
                //表示单元格时段类型（4：上半夜，1：下半夜，6：夜晚，2：上午，3：下午，7：日平均）
                string strCellPeriodType = "3";
                //表示单元格污染物类型（ PM25 = 1,PM10 = 2,NO2 = 3,O3 = 4,O38H = 5, AQI = 6）
                string strCellPolType = "3";
                //每一行内遍历时使用的序号（监测中心）
                int colCycleJCIndex = 0;
                //每一行内遍历时使用的序号（气象局）
                int colCycleQXIndex = 0;
                //每一行内遍历时使用的序号（综合值）
                int colCycleZHIndex = 0;
                string strCellValue = null;
                for (int i = 2; i < cells.MaxDataRow + 1; i++)
                {
                    colCycleJCIndex = 0;
                    colCycleQXIndex = 0;
                    colCycleZHIndex = 0;
                    if (i == 2 || i == 3 || i == 6 || i == 7)
                    {
                        strCellDateType = "3";
                    }
                    else if (i == 4 || i == 5 || i == 8 || i == 9 || i == 10 || i == 11 || i == 12 || i == 13 || i == 16 || i == 17 || i == 22 || i == 23)
                    {
                        strCellDateType = "4";
                    }
                    else if (i == 14 || i == 15 || i == 18 || i == 19 || i == 20 || i == 21 || i == 24 || i == 25)
                    {
                        strCellDateType = "5";
                    }

                    if (i == 2 || i == 3 || i == 12 || i == 13)
                    {
                        strCellPeriodType = "4";
                    }
                    else if (i == 4 || i == 5 || i == 14 || i == 15)
                    {
                        strCellPeriodType = "1";
                    }
                    else if (i == 6 || i == 7 || i == 16 || i == 17)
                    {
                        strCellPeriodType = "6";
                    }
                    else if (i == 8 || i == 9 || i == 18 || i == 19)
                    {
                        strCellPeriodType = "2";
                    }
                    else if (i == 10 || i == 11 || i == 20 || i == 21)
                    {
                        strCellPeriodType = "3";
                    }
                    else if (i == 22 || i == 23 || i == 24 || i == 25)
                    {
                        strCellPeriodType = "7";
                    }


                    if (i % 2 == 0)
                    {
                        strCellType = "P";
                        for (int j = 3; j < cells.MaxDataColumn + 1; j++)
                        {
                            strCellValue = cells[i, j].StringValue.Trim();
                            if (j % 2 == 0)
                            {
                                strCellOrgType = "2";
                                if (j == 4)
                                {
                                    strCellPolType = "1";
                                }
                                if (j == 6)
                                {
                                    strCellPolType = "2";
                                }
                                if (j == 8)
                                {
                                    strCellPolType = "3";
                                }
                                if (j == 10)
                                {
                                    strCellPolType = "4";
                                }
                                if (j == 12)
                                {
                                    strCellPolType = "5";
                                }
                                if (j == 14)
                                {
                                    strCellPolType = "6";
                                }
                            }
                            else
                            {
                                strCellOrgType = "1";
                                if (j == 3)
                                {
                                    strCellPolType = "1";
                                }
                                if (j == 5)
                                {
                                    strCellPolType = "2";
                                }
                                if (j == 7)
                                {
                                    strCellPolType = "3";
                                }
                                if (j == 9)
                                {
                                    strCellPolType = "4";
                                }
                                if (j == 11)
                                {
                                    strCellPolType = "5";
                                }
                                if (j == 13)
                                {
                                    strCellPolType = "6";
                                }

                            }
                            strJson += string.Format("\"{0}{1}{2}{3}{4}\":\"{5}/<span>{6}</span>" + "\",", strCellType, strCellDateType, strCellOrgType, strCellPeriodType, strCellPolType, strCellValue.Split('/')[0], strCellValue.Split('/')[1]);
                        }
                    }
                    else
                    {
                        strCellType = "H";
                        strCellOrgType = "1";
                        for (int m = 3; m < cells.MaxDataColumn; m++)
                        {
                            if (m == 13)
                            {

                            }
                            strCellValue = cells[i, m].StringValue.Trim();
                            if (strCellValue == "" && m < cells.MaxDataColumn)
                            {
                                strCellValue = cells[i, m + 1].StringValue.Trim();
                            }
                            if (m % 2 == 0)
                            {
                                strCellOrgType = "2";
                                if (m == 4)
                                {
                                    strCellPolType = "1";
                                }
                                if (m == 6)
                                {
                                    strCellPolType = "2";
                                }
                                if (m == 8)
                                {
                                    strCellPolType = "3";
                                }
                                if (m == 10)
                                {
                                    strCellPolType = "4";
                                }
                                if (m == 12)
                                {
                                    strCellPolType = "5";
                                }
                                if (m == 14)
                                {
                                    strCellPolType = "6";
                                }
                            }
                            else
                            {
                                strCellOrgType = "1";
                                if (m == 3)
                                {
                                    strCellPolType = "1";
                                }
                                if (m == 5)
                                {
                                    strCellPolType = "2";
                                }
                                if (m == 7)
                                {
                                    strCellPolType = "3";
                                }
                                if (m == 9)
                                {
                                    strCellPolType = "4";
                                }
                                if (m == 11)
                                {
                                    strCellPolType = "5";
                                }
                                if (m == 13)
                                {
                                    strCellPolType = "6";
                                }
                            }
                            strJson += string.Format("\"{0}{1}{2}{3}{4}\":\"{5}/<span>{6}</span>" + "\",", strCellType, strCellDateType, strCellOrgType, strCellPeriodType, strCellPolType, strCellValue.Split('/')[0], strCellValue.Split('/')[1]);
                        }
                    }
                }
                strJson = "{" + strJson.Trim(',') + "}";
                return strJson;
            }
            catch (Exception e)
            {
                return "读取Excel出错！";
            }
        }

        //2015年11月19日，闫海涛，文本文件上传至ftp
        public string UpLoadTxtFtpPast(string txtContent, string strFTPIP, string filePath, string filename)
        {

            byte[] array = System.Text.Encoding.Default.GetBytes(txtContent);
            MemoryStream stream = new MemoryStream(array);
            StreamReader reader = new StreamReader(stream);

            string text = reader.ReadToEnd();
            string strFTPUser = "Admin";
            string strFTPPSW = "gigh06508012";
            filename = @"E:\浦东项目\Test\txt.txt";
            filePath = "20151119";
            FileInfo fileInf = new FileInfo(filename);
            string uri = "ftp://" + strFTPIP + "/" + fileInf.Name;
            FtpWebRequest reqFTP;
            // Create FtpWebRequest object from the Uri provided 
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri("ftp://" + strFTPIP + "/" + filePath + "/" + fileInf.Name));
            try
            {
                // Provide the WebPermission Credintials 
                reqFTP.Credentials = new NetworkCredential(strFTPUser, strFTPPSW);

                // By default KeepAlive is true, where the control connection is not closed 
                // after a command is executed. 
                reqFTP.KeepAlive = false;

                // Specify the command to be executed. 
                reqFTP.Method = WebRequestMethods.Ftp.UploadFile;

                // Specify the data transfer type. 
                reqFTP.UseBinary = true;

                // Notify the server about the size of the uploaded file 
                reqFTP.ContentLength = fileInf.Length;

                // The buffer size is set to 2kb 
                int buffLength = 2048;
                byte[] buff = new byte[buffLength];
                int contentLen;

                // Opens a file stream (System.IO.FileStream) to read the file to be uploaded 
                //FileStream fs = fileInf.OpenRead(); 
                FileStream fs = fileInf.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                // Stream to which the file to be upload is written 
                Stream strm = reqFTP.GetRequestStream();

                // Read from the file stream 2kb at a time 
                contentLen = fs.Read(buff, 0, buffLength);

                // Till Stream content ends 
                while (contentLen != 0)
                {
                    // Write Content from the file stream to the FTP Upload Stream 
                    strm.Write(buff, 0, contentLen);
                    contentLen = fs.Read(buff, 0, buffLength);
                }

                // Close the file stream and the Request Stream 
                strm.Close();
                fs.Close();
                return "success";
            }
            catch (Exception ex)
            {
                reqFTP.Abort();
                //  Logging.WriteError(ex.Message + ex.StackTrace);
                return "fail";
            }
        }

        public string UpLoadTxtFtp(string txtContent, string strFTPIP, string filePath, string filename)
        {
            string strNew = AddHuanhang(txtContent);

            strNew = Regex.Replace(txtContent, "<[^>]+>|&nbsp;|&amp;|&shy;|&#160;|&#173;|&bull;|&lt;|&gt;", " ", RegexOptions.IgnoreCase);

            byte[] array = System.Text.Encoding.Default.GetBytes(strNew);
            //byte[] array = System.Text.Encoding.ASCII.GetBytes(strNew);
            MemoryStream stream = new MemoryStream(array);
            StreamReader reader = new StreamReader(stream);
            string[] vars = txtContent.Split('\n');
            txtContent.Replace('\n', '\r');
            string text = reader.ReadToEnd();
            string strFTPUser = "Admin";
            string strFTPPSW = "gigh06508012";
            filePath = "20151119";
            string uri = "ftp://" + strFTPIP + "/" + filename;
            FtpWebRequest reqFTP;
            // Create FtpWebRequest object from the Uri provided 
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri("ftp://" + strFTPIP + "/" + filePath + "/" + filename));
            try
            {
                reqFTP.Credentials = new NetworkCredential(strFTPUser, strFTPPSW);
                reqFTP.KeepAlive = false;
                reqFTP.Method = WebRequestMethods.Ftp.UploadFile;
                reqFTP.UseBinary = true;
                reqFTP.ContentLength = array.Length;

                Stream strm = reqFTP.GetRequestStream();
                strm.Write(array, 0, array.Length);
                strm.Close();
                return "success";
            }
            catch (Exception ex)
            {
                reqFTP.Abort();
                //  Logging.WriteError(ex.Message + ex.StackTrace);

            }
            return "fail";
        }

        public string UpLoadTxtFtp(string txtContent, string strFTPIP, string strFtpUser, string strftpPwd, string filePath, string filename)
        {
            string strNew = AddHuanhang(txtContent);

            strNew = Regex.Replace(txtContent, "<[^>]+>|&nbsp;|&amp;|&shy;|&#160;|&#173;|&bull;|&lt;|&gt;", " ", RegexOptions.IgnoreCase);

            byte[] array = System.Text.Encoding.Default.GetBytes(strNew);
            //byte[] array = System.Text.Encoding.ASCII.GetBytes(strNew);
            MemoryStream stream = new MemoryStream(array);
            StreamReader reader = new StreamReader(stream);
            string[] vars = txtContent.Split('\n');
            txtContent.Replace('\n', '\r');
            string text = reader.ReadToEnd();
            string strFTPUser = strFtpUser;
            string strFTPPSW = strftpPwd;
            string uri = "ftp://" + strFTPIP + "/" + filename;
            FtpWebRequest reqFTP;
            // Create FtpWebRequest object from the Uri provided 
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri("ftp://" + strFTPIP + "/" + filePath + "/" + filename));
            try
            {
                reqFTP.Credentials = new NetworkCredential(strFTPUser, strFTPPSW);
                reqFTP.KeepAlive = false;
                reqFTP.Method = WebRequestMethods.Ftp.UploadFile;
                reqFTP.UseBinary = true;
                reqFTP.ContentLength = array.Length;

                Stream strm = reqFTP.GetRequestStream();
                strm.Write(array, 0, array.Length);
                strm.Close();
                return "success";
            }
            catch (Exception ex)
            {
                reqFTP.Abort();
                //  Logging.WriteError(ex.Message + ex.StackTrace);

            }
            return "fail";
        }

        //2015年12月4日,根据功能名称在在配置文件中读取FTP用户名和密码，上传txt文件
        public string UpLoadTxtFtpNew(string functionName, string txtContent, string fileName)
        {
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            if (functionName != "")
            {
                strFTPIPString = ConfigurationManager.AppSettings["ftpIP"];
                strFtpIP = strFTPIPString;
                strFTPUser = ConfigurationManager.AppSettings["ftpUser"];
                strFTPPSW = ConfigurationManager.AppSettings["ftpPwd"];
            }


            if (txtContent == "" || txtContent == null)
            {
                return "fail";
            }
            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
            {
                if (strFtpIP.IndexOf('/') > 0)
                {
                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                }

                //string strNew = Regex.Replace(txtContent, "<[^>]+>|&nbsp;|&amp;|&shy;|&#160;|&#173;|&bull;|&lt;|&gt;", " ", RegexOptions.IgnoreCase);
                string strNew = System.Text.RegularExpressions.Regex.Replace(txtContent, "<[^>]*>", "");
                strNew = strNew.Replace("$nbsp", " ");
                strNew = AddHuanhang(txtContent);
                byte[] array = System.Text.Encoding.Default.GetBytes(strNew);
                //byte[] array = System.Text.Encoding.ASCII.GetBytes(strNew);
                MemoryStream stream = new MemoryStream(array);
                StreamReader reader = new StreamReader(stream);
                string[] vars = txtContent.Split('\n');
                txtContent.Replace('\n', '\r');
                string text = reader.ReadToEnd();

                string uri = "ftp://" + strFTPIPString + "/" + fileName;
                FtpWebRequest reqFTP;
                // Create FtpWebRequest object from the Uri provided 
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(uri));
                try
                {
                    reqFTP.Credentials = new NetworkCredential(strFTPUser, strFTPPSW);
                    reqFTP.KeepAlive = false;
                    reqFTP.Method = WebRequestMethods.Ftp.UploadFile;
                    reqFTP.UseBinary = true;
                    reqFTP.ContentLength = array.Length;

                    Stream strm = reqFTP.GetRequestStream();
                    strm.Write(array, 0, array.Length);
                    strm.Close();
                    return "success";
                }
                catch (Exception ex)
                {
                    reqFTP.Abort();
                    //  Logging.WriteError(ex.Message + ex.StackTrace);

                }
            }

            return "fail";
        }

        //给字符串添加换行符
        private string AddHuanhang(string input)
        {
            if (input != null)
            {
                StringBuilder sb = new StringBuilder();
                string[] strParts = input.Split('\n');
                for (int i = 0; i < strParts.Length; i++)
                {
                    sb.Append(strParts[i]);
                    sb.Append("\r\n");
                }
                return sb.ToString();
            }
            return null;
        }

        //2015年11月20日  获取分区AQI以及首要污染物
        
        public string QueryAreaAQICopy()
        {
            DateTime dtNow = DateTime.Now.Date;
            dtNow = dtNow.AddDays(1);
            //if (forecastDate != "")
            //    dtNow = DateTime.Parse(forecastDate);
            string forecastDateTime = dtNow.ToString("yyyy-MM-dd 20:00:00");

            //string strSQL = "select m.* from  ( select Max(AQI) AS AQI,Site From(select Site,LST,ITEMID,AQI from T_ForecastSite  WHERE  Site in ('58367','58361','58365','58362','58462','58460','58461','58370','58463','58366') AND durationID=7 AND LST='" + forecastDateTime + "' ) result GROUP BY result.Site  ) t , ( select Site,LST,ITEMID,AQI ,[ForecastDate]from T_ForecastSite  WHERE  Site in ('58367','58370','58361','58362','58462','58460','58461','58463','58365','58366') AND durationID=7 AND LST='" + forecastDateTime + "' ) m where t.AQI=m.AQI and t.Site=m.Site";
            string strSQL = "select m.* from  ( select Max(AQI) AS AQI,Site From(select Site,LST,ITEMID,AQI from T_ForecastSite  WHERE  Site in ('58367','58361','58365','58362','58462','58460','58461','58370','58463','58366') AND durationID=7 AND LST='" + forecastDateTime + "' and ITEMID <>5) result GROUP BY result.Site  ) t , ( select Site,LST,ITEMID,AQI ,[ForecastDate]from T_ForecastSite  WHERE  Site in ('58367','58370','58361','58362','58462','58460','58461','58463','58365','58366') AND durationID=7 AND LST='" + forecastDateTime + "' and ITEMID <>5) m where t.AQI=m.AQI and t.Site=m.Site";

            StringBuilder sb = new StringBuilder("{");
            DataSet ds = m_Database.GetDataset(strSQL);

            string json = "";
            if (ds.Tables.Count > 0)
            {
                for (int i = 0; i < ds.Tables.Count; i++)
                {
                    DataTable dTable = ds.Tables[i];

                    //生成实况，综合预报，模式数据的json
                    if (dTable.Rows.Count > 0)
                    {
                        //闫海涛修改
                        json = GetAQIJson(dTable);

                    }
                }

            }
            return json;
        }

        public string QueryAreaAQI()
        {
            DateTime dtNow = DateTime.Now.Date;
            dtNow = dtNow.AddDays(1);
            //if (forecastDate != "")
            //    dtNow = DateTime.Parse(forecastDate);

            string strMaxForeDate = "";
            string strMaxDateSQL = "select MAX(ForecastDate) from dbo.T_ForecastSite";

            DataTable dtMax = m_Database.GetDataTable(strMaxDateSQL);
            if (dtMax.Rows.Count > 0)
            {
                strMaxForeDate = dtMax.Rows[0][0].ToString();
            }
            string forecastDateTime = dtNow.ToString("yyyy-MM-dd 20:00:00");            
            //string strSQL = "select m.* from  ( select Max(AQI) AS AQI,Site From(select Site,LST,ITEMID,AQI from T_ForecastSite  WHERE  Site in ('58367','58361','58365','58362','58462','58460','58461','58370','58463','58366') AND durationID=7 AND LST='" + forecastDateTime + "' and ITEMID <>5) result GROUP BY result.Site  ) t , ( select Site,LST,ITEMID,AQI ,[ForecastDate]from T_ForecastSite  WHERE  Site in ('58367','58370','58361','58362','58462','58460','58461','58463','58365','58366') AND durationID=7 AND LST='" + forecastDateTime + "' and ITEMID <>5) m where t.AQI=m.AQI and t.Site=m.Site";
            string strSQL = "select m.* from  ( select Max(AQI) AS AQI,Site From(select Site,LST,ITEMID,AQI from T_ForecastSite  WHERE  Site in ('58367','58361','58365','58362','58462','58460','58461','58370','58463','58366') AND durationID=7 AND LST='" + forecastDateTime + "' and ForecastDate='" + strMaxForeDate + "' and ITEMID <>5) result GROUP BY result.Site  ) t , ( select Site,LST,ITEMID,AQI ,[ForecastDate]from T_ForecastSite  WHERE  Site in ('58367','58370','58361','58362','58462','58460','58461','58463','58365','58366') AND durationID=7 AND LST='" + forecastDateTime + "' and ForecastDate='" + strMaxForeDate + "' and ITEMID <>5) m where t.AQI=m.AQI and t.Site=m.Site";

            StringBuilder sb = new StringBuilder("{");
            DataSet ds = m_Database.GetDataset(strSQL);

            string json = "";
            if (ds.Tables.Count > 0)
            {
                for (int i = 0; i < ds.Tables.Count; i++)
                {
                    DataTable dTable = ds.Tables[i];

                    //生成实况，综合预报，模式数据的json
                    if (dTable.Rows.Count > 0)
                    {
                        //闫海涛修改
                        json = GetAQIJson(dTable);

                    }
                }

            }
            return json;
        }

        //AQI分区，根据查询结果，返回json
        private string GetAQIJsonCopy(DataTable dataTable)
        {
            StringBuilder sb = new StringBuilder();
            string strItem = "";
            //存储污染等级的json序列
            StringBuilder strPolLevelJson = new StringBuilder("{");
            //存储首要污染物的json序列
            StringBuilder strFirstPolJson = new StringBuilder("{");
            //存储AQIjson序列
            StringBuilder strAQIJson = new StringBuilder("{");
            //存储AQI的序列
            StringBuilder strHazeJson = new StringBuilder("{");
            //存储污染等级颜色json序列
            StringBuilder strColorJson = new StringBuilder("{");
            foreach (DataRow row in dataTable.Rows)
            {
                string strAreaID = row[0].ToString();
                string strItemID = row[2].ToString();
                string strAQI = row[3].ToString();
                string strAQILevel = CalculateAQLLevel(strAQI);
                //污染等级对应数字
                string strAQILevelNO = CalculateAQLLevelNo(strAQI).ToString();
                ;
                //switch (strItemID)
                //{
                //    case "6":
                //        strItem = "PM2.5";
                //        break;
                //    case "3":
                //        strItem = "PM10";
                //        break;
                //    case "2":
                //        strItem = "NO2";
                //        break;
                //    case "5":
                //        strItem = "O3-1小时";
                //        break;
                //    case "4":
                //        strItem = "CO";
                //        break;
                //    case "1":
                //        strItem = "SO2";
                //        break;
                //    default:
                //        strItem = "PM2.5";
                //        break;
                //}
                switch (strItemID)
                {
                    case "6":
                        strItem = "CO";
                        break;
                    case "3":
                        strItem = "NO2";
                        break;
                    case "2":
                        strItem = "PM10";
                        break;
                    case "5":
                        strItem = "O3_8h";
                        break;
                    case "4":
                        strItem = "O3";
                        break;
                    case "1":
                        strItem = "PM2.5";
                        break;
                    default:
                        strItem = "PM2.5";
                        break;
                }
                strPolLevelJson.Append("\"" + strAreaID + "_Level\":\"" + strAQILevel + "\",");
                strFirstPolJson.Append("\"" + strAreaID + "_Item\":\"" + strItem + "\",");
                strAQIJson.Append("\"" + strAreaID + "_AQI\":\"" + strAQI + "\",");
                strColorJson.Append("\"" + strAreaID + "\":\"" + strAQILevelNO + "\",");
                //sb.Append("'" + strAreaID + "_Item':'" + strItem + "','" + strAreaID + "_AQI':'" + strAQI + "','" + strAreaID + "_Level':'" + strAQILevel + "','" + strAreaID + "_ColorNo':'" + strAQILevelNO + "',");                
            }
            //去掉多余的“,”
            //if (sb.Length > 1)
            //{
            //    sb.Remove(sb.Length - 1, 1);                
            //}
            if (strPolLevelJson.Length > 1)
            {
                strPolLevelJson.Remove(strPolLevelJson.Length - 1, 1);
                strPolLevelJson.Append("}");
            }
            if (strFirstPolJson.Length > 1)
            {
                strFirstPolJson.Remove(strFirstPolJson.Length - 1, 1);
                strFirstPolJson.Append("}");
            }
            if (strAQIJson.Length > 1)
            {
                strAQIJson.Remove(strAQIJson.Length - 1, 1);
                strAQIJson.Append("}");
            }
            if (strColorJson.Length > 1)
            {
                strColorJson.Remove(strColorJson.Length - 1, 1);
                strColorJson.Append("}");
            }
            sb.Append("{");
            sb.Append("\"PolLevel\":" + strPolLevelJson.ToString() + ",");
            sb.Append("\"FirstPol\":" + strFirstPolJson.ToString() + ",");
            sb.Append("\"AQI\":" + strAQIJson.ToString() + ",");
            sb.Append("\"LevelColor\":" + strColorJson.ToString() + "}");
            return sb.ToString();
        }

        private string GetAQIJson(DataTable dataTable)
        {
            StringBuilder sb = new StringBuilder();
            string strItem = "";
            //存储污染等级的json序列
            StringBuilder strPolLevelJson = new StringBuilder("{");
            //存储首要污染物的json序列
            StringBuilder strFirstPolJson = new StringBuilder("{");
            //存储AQIjson序列
            StringBuilder strAQIJson = new StringBuilder("{");
            //存储AQI的序列
            StringBuilder strHazeJson = new StringBuilder("{");
            //存储污染等级颜色json序列
            StringBuilder strColorJson = new StringBuilder("{");
            foreach (DataRow row in dataTable.Rows)
            {
                string strAreaID = row[0].ToString();
                string strItemID = row[2].ToString();
                string strAQI = row[3].ToString();
                string strAQILevel = CalculateAQLLevel(strAQI);
                //污染等级对应数字
                string strAQILevelNO = CalculateAQLLevelNo(strAQI).ToString();
                ;
                //switch (strItemID)
                //{
                //    case "6":
                //        strItem = "PM2.5";
                //        break;
                //    case "3":
                //        strItem = "PM10";
                //        break;
                //    case "2":
                //        strItem = "NO2";
                //        break;
                //    case "5":
                //        strItem = "O3-1小时";
                //        break;
                //    case "4":
                //        strItem = "CO";
                //        break;
                //    case "1":
                //        strItem = "SO2";
                //        break;
                //    default:
                //        strItem = "PM2.5";
                //        break;
                //}
                switch (strItemID)
                {
                    case "6":
                        strItem = "CO";
                        break;
                    case "3":
                        strItem = "NO2";
                        break;
                    case "2":
                        strItem = "PM10";
                        break;
                    case "5":
                        strItem = "O3-8h";
                        break;
                    case "4":
                        strItem = "O3-1h";
                        break;
                    case "1":
                        strItem = "PM2.5";
                        break;
                    default:
                        strItem = "PM2.5";
                        break;
                }
                strPolLevelJson.Append("\"" + strAreaID + "_Level\":\"" + strAQILevel + "\",");
                strFirstPolJson.Append("\"" + strAreaID + "_Item\":\"" + strItem + "\",");
                strAQIJson.Append("\"" + strAreaID + "_AQI\":\"" + strAQI + "\",");
                strColorJson.Append("\"" + strAreaID + "\":\"" + strAQILevelNO + "\",");
                //sb.Append("'" + strAreaID + "_Item':'" + strItem + "','" + strAreaID + "_AQI':'" + strAQI + "','" + strAreaID + "_Level':'" + strAQILevel + "','" + strAreaID + "_ColorNo':'" + strAQILevelNO + "',");                
            }
            //去掉多余的“,”
            //if (sb.Length > 1)
            //{
            //    sb.Remove(sb.Length - 1, 1);                
            //}
            if (strPolLevelJson.Length > 1)
            {
                strPolLevelJson.Remove(strPolLevelJson.Length - 1, 1);
                strPolLevelJson.Append("}");
            }
            if (strFirstPolJson.Length > 1)
            {
                strFirstPolJson.Remove(strFirstPolJson.Length - 1, 1);
                strFirstPolJson.Append("}");
            }
            if (strAQIJson.Length > 1)
            {
                strAQIJson.Remove(strAQIJson.Length - 1, 1);
                strAQIJson.Append("}");
            }
            if (strColorJson.Length > 1)
            {
                strColorJson.Remove(strColorJson.Length - 1, 1);
                strColorJson.Append("}");
            }
            sb.Append("{");
            sb.Append("\"PolLevel\":" + strPolLevelJson.ToString() + ",");
            sb.Append("\"FirstPol\":" + strFirstPolJson.ToString() + ",");
            sb.Append("\"AQI\":" + strAQIJson.ToString() + ",");
            sb.Append("\"LevelColor\":" + strColorJson.ToString() + "}");
            return sb.ToString();
        }

        //计算AQI等级（优，良，轻度，重度，严重）
        public string CalculateAQLLevel(string aqiValue)
        {
            string strAQLLevel = "";
            if (aqiValue != null)
            {
                int intAQI = Convert.ToInt32(aqiValue);
                if (intAQI > 0 && intAQI <= 50)
                {
                    strAQLLevel = "优";
                }
                else if (intAQI > 50 && intAQI <= 100)
                {
                    strAQLLevel = "良";
                }
                else if (intAQI > 100 && intAQI <= 150)
                {
                    strAQLLevel = "轻度污染";
                }
                else if (intAQI > 150 && intAQI <= 200)
                {
                    strAQLLevel = "中度污染";
                }
                else if (intAQI > 200 && intAQI <= 300)
                {
                    strAQLLevel = "重度污染";
                }
                else if (intAQI > 300)
                {
                    strAQLLevel = "严重污染";
                }
            }
            return strAQLLevel;
        }

        //计算AQI等级对应的编码
        public int CalculateAQLLevelNo(string aqiValue)
        {
            int intAQLLevel = 0;
            if (aqiValue != null)
            {
                int intAQI = Convert.ToInt32(aqiValue);
                if (intAQI > 0 && intAQI <= 50)
                {
                    intAQLLevel = 1;
                }
                else if (intAQI > 50 && intAQI <= 100)
                {
                    intAQLLevel = 2;
                }
                else if (intAQI > 100 && intAQI <= 150)
                {
                    intAQLLevel = 3;
                }
                else if (intAQI > 150 && intAQI <= 200)
                {
                    intAQLLevel = 4;
                }
                else if (intAQI > 200 && intAQI <= 300)
                {
                    intAQLLevel = 5;
                }
                else if (intAQI > 300)
                {
                    intAQLLevel = 6;
                }
            }
            return intAQLLevel;
        }

        //2015年11月22日，霾预报入库        

        public string SaveHazeForecastDataCopy(string todayDate, string tomorrowDate, string afterDate, string todayHaze, string tomorrowHaze, string afterHaze, string todayVis, string tomorrowVis, string afterVis, string releaseTime, string hourType)
        {
            try
            {
                string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                string functionName = "HazeForecast_05";
                if (hourType == "05")
                {
                    strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 05:00:00.000");
                    strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 08:00:00.000");
                    functionName = "HazeForecast_05";
                }
                else if (hourType == "17")
                {
                    strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                    functionName = "HazeForecast_17";
                }
                string strToday = ConvertDatetime(todayDate);
                string strTomToday = ConvertDatetime(tomorrowDate);
                string strAfterToday = ConvertDatetime(afterDate);
                if (IsPublished(functionName, strRcdTime) == true)
                {
                    return "published";
                }
                string strSQL = string.Format("INSERT INTO T_Haze (LST, ReTime, Haze,Vis) SELECT '{0}','{9}','{3}','{6}' UNION ALL SELECT  '{1}','{9}','{4}','{7}' UNION ALL SELECT  '{2}','{9}','{5}','{8}'",
                    strToday, strTomToday, strAfterToday, todayHaze, tomorrowHaze, afterHaze, todayVis, tomorrowVis, afterVis, releaseTime);
                //m_Database.Execute("truncate table T_Haze");
                m_Database.Execute("delete from T_Haze where ReTime='" + releaseTime + "'");
                m_Database.Execute(strSQL);

                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "1", "2");
                return "success";
            }
            catch (Exception e)
            {
                return "fail";
            }

        }

        public string SaveHazeForecastData(string todayDate, string tomorrowDate, string afterDate, string todayHaze, string tomorrowHaze, string afterHaze, string todayVis, string tomorrowVis, string afterVis, string releaseTime, string hourType)
        {
            try
            {
                string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                string functionName = "HazeForecast_05";
                if (hourType == "05")
                {
                    strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 05:00:00.000");
                    strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 08:00:00.000");
                    functionName = "HazeForecast_05";
                    releaseTime = DateTime.Now.ToString("yyyy-MM-dd 05:00:00.000");
                }
                else if (hourType == "17")
                {
                    strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                    functionName = "HazeForecast_17";
                    releaseTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                }
                string strToday = ConvertDatetime(todayDate);
                string strTomToday = ConvertDatetime(tomorrowDate);
                string strAfterToday = ConvertDatetime(afterDate);
                if (IsPublished(functionName, strRcdTime) == true)
                {
                    return "published";
                }
                string strSQL = string.Format("INSERT INTO T_Haze (LST, ReTime, Haze,Vis) SELECT '{0}','{9}','{3}','{6}' UNION ALL SELECT  '{1}','{9}','{4}','{7}' UNION ALL SELECT  '{2}','{9}','{5}','{8}'",
                    strToday, strTomToday, strAfterToday, todayHaze, tomorrowHaze, afterHaze, todayVis, tomorrowVis, afterVis, releaseTime);
                //m_Database.Execute("truncate table T_Haze");
                m_Database.Execute("delete from T_Haze where ReTime='" + releaseTime + "'");
                m_Database.Execute(strSQL);

                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "1", "2");
                return "success";
            }
            catch (Exception e)
            {
                return "fail";
            }

        }

        public string SaveHazeForecastDataMerge(string todayDate, string tomorrowDate, string afterDate, string todayHaze, string tomorrowHaze, string afterHaze, string todayVis, string tomorrowVis, string afterVis, string releaseTime, string hourType, string tomHaze24, string tomVis24, string userName)
        {
            try
            {
                string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                string functionName = "HazeForecast_05";
                if (hourType == "05")
                {
                    strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 05:00:00.000");
                    strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 08:00:00.000");
                    functionName = "HazeForecast_05";
                }
                else if (hourType == "17")
                {
                    strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                    functionName = "HazeForecast_17";
                }
                string strToday = ConvertDatetime(todayDate);
                string strTomToday = ConvertDatetime(tomorrowDate);
                string strAfterToday = ConvertDatetime(afterDate);
                //if (IsPublished(functionName, strRcdTime) == true)
                //{
                //    return "published";
                //}
                string strSQL = string.Format("INSERT INTO T_Haze (LST, ReTime, Haze,Vis) SELECT '{0}','{9}','{3}','{6}' UNION ALL SELECT  '{1}','{9}','{4}','{7}' UNION ALL SELECT  '{2}','{9}','{5}','{8}'",
                    strToday, strTomToday, strAfterToday, todayHaze, tomorrowHaze, afterHaze, todayVis, tomorrowVis, afterVis, releaseTime);
                //m_Database.Execute("truncate table T_Haze");
                m_Database.Execute("delete from T_Haze where ReTime='" + releaseTime + "'");
                m_Database.Execute(strSQL);

                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "1", "2");
               
                //保存24小时霾预报
                strSQL = string.Format("INSERT INTO T_24Haze (LST,Haze,Vis,UserName) SELECT '{0}','{1}','{2}','{3}'", DateTime.Now.AddDays(1).ToString("yyyy-MM-dd 00:00:00.000"), tomHaze24, tomVis24, userName);
                m_Database.Execute("delete from T_24Haze where LST='" + DateTime.Now.AddDays(1).ToString("yyyy-MM-dd 00:00:00.000") + "'");
                m_Database.Execute(strSQL);
                return "success";
            }
            catch (Exception e)
            {
                return "fail";
            }

        }
        
        public string SaveHazeForecastData_24(string hourType, string tomHaze24,string userName)
        {
            try
            {
                string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 05:00:00.000");
                string strLSTTime = DateTime.Now.ToString("yyyy-MM-dd 00:00:00.000");                
                if (hourType == "05")
                {
                    strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 05:00:00.000");
                    strLSTTime = DateTime.Now.ToString("yyyy-MM-dd 00:00:00.000");
                    
                }
                else if (hourType == "17")
                {
                    strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    strLSTTime = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd 00:00:00.000");
                   
                }


                //保存24小时霾预报
                //string strSQL = string.Format("INSERT INTO T_24Haze (LST,ReTime,Haze,UserName) SELECT '{0}','{1}','{2}','{3}'", strLSTTime,strRcdTime,tomHaze24, userName);
                //m_Database.Execute("delete from T_24Haze where LST='" + DateTime.Now.AddDays(1).ToString("yyyy-MM-dd 00:00:00.000") + "'");
                //m_Database.Execute(strSQL);
                string strSQL = string.Format("INSERT INTO T_24Haze (LST,ReTime,Haze,UserName) SELECT '{0}','{1}','{2}','{3}'", strLSTTime, strRcdTime, tomHaze24, userName);
                m_Database.Execute("delete from T_24Haze where ReTime='" + strRcdTime + "'");
                m_Database.Execute(strSQL);
                return "success";
            }
            catch (Exception e)
            {
                return "fail";
            }

        }


        //2015年12月1日，发布上传霾预报文件
        public string PublishHazeForecast(string content, string strFTPIP, string filePath, string filename)
        {
            return UpLoadTxtFtp(content, strFTPIP, filePath, filename);
        }

        //2015年11月23日，紫外线实况数据获取
        public string GetUVRealDataTest(string currentDate)
        {
            Random rand = new Random();
            int value = rand.Next(1, 90);
            string strUVValue = value.ToString();
            return "99999";
        }

        public string GetUVRealData(string currentDate)
        {
            string strUVValue = "0";
            try
            {
                DateTime dtNow = DateTime.Now;
                string strStart = dtNow.ToString("yyyy-MM-dd 10:00:00");
                string strEnd = dtNow.ToString("yyyy-MM-dd 14:00:00");
                Database uvDatabase = new Database("UVDBCONFIG");
                string strSQL = "SELECT  avg(UVS_AB) FROM dbo.tbUVS where DateTime>='" + strStart + "' and DateTime<='" + strEnd + "'" + " and StationID='58370'";
                DataSet ds = uvDatabase.GetDataset(strSQL);
                if (ds.Tables.Count > 0)
                {
                    for (int i = 0; i < ds.Tables.Count; i++)
                    {
                        DataTable dataTable = ds.Tables[0];
                        double intValue = Convert.ToDouble(dataTable.Rows[0][0].ToString());
                        //strUVValue = Math.Round(intValue, 2).ToString("0.0");
                        strUVValue = Math.Round(intValue, 1).ToString();
                    }
                }
                return strUVValue;
            }
            catch (Exception e) { }
            finally
            {

            }
            return strUVValue;
        }


        //2015年11月23日，紫外线预报入库
        public string SaveUVForecastDataPast(string dateTime, string UVAB)
        {
            string stationID = "58367";
            try
            {
                string strSQL = string.Format("INSERT INTO T_TbUVS (StationID, DateTime, UVAB) VALUES ('{0}', '{1}', '{2}')", stationID, dateTime, UVAB);
                m_Database.Execute(strSQL);
                //string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");

                string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 09:45:00.000");
                string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 12:45:00.000");
                InsertIntoStateTable("UVForecast", strRcdTime, strDeadLineTime, "1", "2");
                return "success";
            }
            catch (Exception e)
            {
                return "fail";
            }

        }

        public string SaveUVForecastDataPast(string dateTime, string UVAB, string uvIndex)
        {
            string stationID = "58367";
            try
            {
                string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 09:45:00.000");
                string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 12:45:00.000");
                if (IsPublished("UVForecast", strRcdTime) == true)
                {
                    return "published";
                }
                string strSQL = string.Format("INSERT INTO T_TbUVS (StationID, LST, UVAB,[Index]) VALUES ('{0}', '{1}', '{2}','{3}')", stationID, dateTime, UVAB, uvIndex);
                m_Database.Execute("delete from T_TbUVS where LST='" + dateTime + "'");
                m_Database.Execute(strSQL);

                InsertIntoStateTable("UVForecast", strRcdTime, strDeadLineTime, "1", "2");
                return "success";
            }
            catch (Exception e)
            {
                return "fail";
            }

        }


        public string SaveUVForecastDataCopy(string dateTime, string UVAB, string uvIndex, string fileDate, string tomorrowContent, string userName)
        {
            string stationID = "58367";
            try
            {
                string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 09:45:00.000");
                string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 12:45:00.000");
                //if (IsPublished("UVForecast", strRcdTime) == true)
                //{
                //    return "published";
                //}
                string strSQL = "";
                //m_Database.Execute("delete from T_TbUVS where LST='" + dateTime + "'");
                //m_Database.Execute(strSQL);

                //上传第二天的文本
                //UpLoadTomorrowUV(fileDate, tomorrowContent, userName);

                //上传第二天的文本
                DateTime dtNow = DateTime.Now;
                DateTime dt = DateTime.Now;
                dt = dt.AddDays(1);
                if (dtNow.Hour < 10)
                {                   
                    
                    strSQL = string.Format("INSERT INTO T_TbUVS (StationID, LST, UVAB,[Index],ReTime) SELECT '{0}', '{1}', '{2}','{3}','{4}'", stationID, dt.ToString("yyyy-MM-dd 00:00:00.000"), UVAB, uvIndex, DateTime.Now.ToString("yyyy-MM-dd 10:00:00.000"));
                    strSQL += " UNION ALL SELECT '"+stationID+"','"+ dt.ToString("yyyy-MM-dd 00:00:00.000")+"','"+ UVAB+"','"+ uvIndex+"','"+ DateTime.Now.AddDays(1).ToString("yyyy-MM-dd 10:00:00.000")+"'";
                    m_Database.Execute("delete from T_TbUVS where LST='" + dt.ToString("yyyy-MM-dd 00:00:00.000") + "'");
                    m_Database.Execute(strSQL);
                    UpLoadTomorrowUVAdjustToday(fileDate, tomorrowContent, userName);
                }
                else
                {
                    //strSQL = string.Format("INSERT INTO T_TbUVS (StationID, LST, UVAB,[Index]) VALUES ('{0}', '{1}', '{2}','{3}')", stationID, dateTime, UVAB, uvIndex);
                    strSQL = string.Format("INSERT INTO T_TbUVS (StationID, LST, UVAB,[Index],ReTime) SELECT '{0}', '{1}', '{2}','{3}','{4}'", stationID, dt.ToString("yyyy-MM-dd 00:00:00.000"), UVAB, uvIndex, DateTime.Now.ToString("yyyy-MM-dd 10:00:00.000"));
                    strSQL += " UNION ALL SELECT '" + stationID + "','" + dt.ToString("yyyy-MM-dd 00:00:00.000") + "','" + UVAB + "','" + uvIndex + "','" + DateTime.Now.ToString("yyyy-MM-dd 16:00:00.000") + "'";
                    m_Database.Execute("delete from T_TbUVS where LST='" + dt.ToString("yyyy-MM-dd 00:00:00.000") + "'");
                    m_Database.Execute(strSQL);
                    //InsertIntoStateTable("UVForecast", strRcdTime, strDeadLineTime, "1", "2");
                }

                return "success";
            }
            catch (Exception e)
            {
                return "fail";
            }

        }

        public string SaveUVForecastData(string dateTime, string UVAB, string uvIndex, string fileDate, string tomorrowContent, string userName)
        {
            string stationID = "58367";
            try
            {
                string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 09:45:00.000");
                string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 12:45:00.000");
                //if (IsPublished("UVForecast", strRcdTime) == true)
                //{
                //    return "published";
                //}
                string strSQL = "";
                //m_Database.Execute("delete from T_TbUVS where LST='" + dateTime + "'");
                //m_Database.Execute(strSQL);

                //上传第二天的文本
                //UpLoadTomorrowUV(fileDate, tomorrowContent, userName);

                //上传第二天的文本
                DateTime dtNow = DateTime.Now;
                DateTime dt = DateTime.Now;
                dt = dt.AddDays(1);
                if (dtNow.Hour < 10)
                {

                    strSQL = string.Format("INSERT INTO T_TbUVS (StationID, LST, UVAB,[Index],ReTime,UserName) SELECT '{0}', '{1}', '{2}','{3}','{4}','{5}'", stationID, dt.ToString("yyyy-MM-dd 00:00:00.000"), UVAB, uvIndex, DateTime.Now.ToString("yyyy-MM-dd 10:00:00.000"),userName);
                    strSQL += " UNION ALL SELECT '" + stationID + "','" + dt.ToString("yyyy-MM-dd 00:00:00.000") + "','" + UVAB + "','" + uvIndex + "','" + DateTime.Now.AddDays(1).ToString("yyyy-MM-dd 10:00:00.000") + "','"+userName+"'";
                    m_Database.Execute("delete from T_TbUVS where LST='" + dt.ToString("yyyy-MM-dd 00:00:00.000") + "'");
                    m_Database.Execute(strSQL);
                    UpLoadTomorrowUVAdjustToday(fileDate, tomorrowContent, userName);
                }
                else
                {
                    //strSQL = string.Format("INSERT INTO T_TbUVS (StationID, LST, UVAB,[Index]) VALUES ('{0}', '{1}', '{2}','{3}')", stationID, dateTime, UVAB, uvIndex);
                    strSQL = string.Format("INSERT INTO T_TbUVS (StationID, LST, UVAB,[Index],ReTime,UserName) SELECT '{0}', '{1}', '{2}','{3}','{4}','{5}'", stationID, dt.ToString("yyyy-MM-dd 00:00:00.000"), UVAB, uvIndex, DateTime.Now.ToString("yyyy-MM-dd 10:00:00.000"), userName);
                    strSQL += " UNION ALL SELECT '" + stationID + "','" + dt.ToString("yyyy-MM-dd 00:00:00.000") + "','" + UVAB + "','" + uvIndex + "','" + DateTime.Now.ToString("yyyy-MM-dd 16:00:00.000") +"','"+userName+ "'";
                    m_Database.Execute("delete from T_TbUVS where LST='" + dt.ToString("yyyy-MM-dd 00:00:00.000") + "'");
                    m_Database.Execute(strSQL);
                    //InsertIntoStateTable("UVForecast", strRcdTime, strDeadLineTime, "1", "2");
                }

                return "success";
            }
            catch (Exception e)
            {
                return "fail";
            }

        }

        //2015年11月23日，获取上一次紫外线数据
        public string LoadLastUVValue()
        {
            try
            {
                string strSQL = string.Format("select ");
                //m_Database.Execute(strSQL);
                return "success";
            }
            catch (Exception e)
            {
                return "fail";
            }
        }

        //获取O3和O3_8H最大的值
        public string GetOzoneData(string forecastDate)
        {
            string siteID = "58367";
            string aqiItemO3 = "4";
            string aqiItemO38 = "5";
            DateTime dtNow = DateTime.Now.Date;
            if (forecastDate != "")
                dtNow = DateTime.Parse(forecastDate).AddDays(1);
            //查询时间段起点
            string LSTStart = dtNow.ToString("yyyy-MM-dd HH:00:00");
            //查询时间段终点
            string LSTEnd = dtNow.AddHours(23).ToString("yyyy-MM-dd HH:00:00");
            string strSQL = string.Format("select f.ITEMID, max(f.Value)  from  (select * from T_ForecastSite,(select MAX(ForecastDate) maxtime from T_ForecastSite)  a where ForecastDate=a.maxtime and LST >='{0}' and LST<='{1}' and Site={2} and (ITEMID={3} or ITEMID={4}) ) f group by f.ITEMID", LSTStart, LSTEnd, siteID, aqiItemO3, aqiItemO38);
            DataSet ds = m_Database.GetDataset(strSQL);
            StringBuilder sb = new StringBuilder();
            if (ds.Tables.Count > 0)
            {
                for (int i = 0; i < ds.Tables.Count; i++)
                {
                    DataTable dTable = ds.Tables[i];
                    if (dTable.Rows.Count > 0)
                    {
                        string strItemName = "";
                        for (int j = 0; j < dTable.Rows.Count; j++)
                        {
                            //O3一小时平均值
                            if (dTable.Rows[j][0].ToString() == "4")
                            {
                                strItemName = "O3";
                            }
                            //O3 8小时平均值
                            else if (dTable.Rows[j][0].ToString() == "5")
                            {
                                strItemName = "O3_8";
                            }
                            sb.Append(strItemName);
                            sb.Append(":");
                            sb.Append(dTable.Rows[j][1].ToString());
                            sb.Append(",");
                        }

                        //去掉多余的“,”
                        if (sb.Length > 1)
                        {
                            sb.Remove(sb.Length - 1, 1);
                        }
                    }
                }
            }
            return "{" + sb.ToString() + "}";
        }

        public string GetOzoneDataTest(string forecastDate)
        {

            return "{O3:83,O3_8:108}";
        }

        //2015年11月24日，保存臭氧数据
        public string SaveOzoneData(string releaseTime, string o3Value, string o38Value, string o3Period, string o38Period)
        {

            try
            {
                string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:40:00.000");
                string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 19:40:00.000");
                if (IsPublished("OzoneForecast", strRcdTime) == true)
                {
                    return "published";
                }
                DateTime dtNow = DateTime.Now;
                string strLST = dtNow.AddDays(1).ToString("yyyy-MM-dd 00:00:00.000");
                if (o3Value == "")
                {
                    o3Value = "0";
                }
                if (o38Value == "")
                {
                    o38Value = "0";
                }
                string strSQL = string.Format("INSERT INTO T_Ozone (LST, ReTime,O3,O38,O3Period,O38Period) VALUES ('{0}', '{1}', '{2}','{3}','{4}','{5}')", strLST, releaseTime, o3Value, o38Value, o3Period, o38Period);
                m_Database.Execute("delete from T_Ozone where LST='" + strLST + "'");
                m_Database.Execute(strSQL);

                InsertIntoStateTable("OzoneForecast", strRcdTime, strDeadLineTime, "1", "2");

                return "success";
            }
            catch (Exception e)
            {
                return "fail";
            }
        }

        //获取空气污染气象条件数据

        public string GetAirPollutionForecastCopy(string forecastDate)
        {
            DateTime dtNow = DateTime.Now.Date;
            if (forecastDate != "")
                dtNow = DateTime.Parse(forecastDate);
            string forecastDateTime = dtNow.ToString("yyyy-MM-dd HH:00:00");
            //正式语句
            string strSQL = String.Format("select Site,AQI from T_ForecastSite , (select MAX(ForecastDate) maxtime from T_ForecastSite)  maxDate WHERE ForecastDate = maxDate.maxtime AND Site in ('58367','58361','58365','58362','58462','58460','58461','58370','58463','58366') AND durationID=7 AND ITEMID=1 AND LST='{0}'", forecastDateTime);

            //测试语句
            //string strSQL = String.Format("select * from AQITest");
            StringBuilder sb = new StringBuilder("{");
            DataSet ds = m_Database.GetDataset(strSQL);

            string style = "";
            string json = "";
            //DataTable dTable = m_Database.GetDataTable(strSQL);
            if (ds.Tables.Count > 0)
            {
                string strSingle = "";
                for (int i = 0; i < ds.Tables.Count; i++)
                {
                    DataTable dTable = ds.Tables[i];

                    //生成实况，综合预报，模式数据的json
                    if (dTable.Rows.Count > 0)
                    {
                        for (int j = 0; j < dTable.Rows.Count; j++)
                        {
                            //创建json，便于前台赋值
                            strSingle = "\"" + dTable.Rows[j]["Site"].ToString() + "\":\"" + dTable.Rows[j]["AQI"].ToString() + "\",";
                            sb.Append(strSingle);
                        }

                    }
                }

            }
            //sb.Append(string.Format("nowDateTime:'{0}'", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            if (sb.Length > 1)
            {
                sb.Remove(sb.Length - 1, 1);
                sb.Append("}");
            }
            else
                sb.Length = 0;

            return sb.ToString();
        }

        public string GetAirPollutionForecast(string forecastDate)
        {
            DateTime dtNow = DateTime.Now.Date;
            if (forecastDate != "")
                dtNow = DateTime.Parse(forecastDate);
            string forecastDateTime = dtNow.ToString("yyyy-MM-dd HH:00:00");

            string strMaxDateSQL = "select MAX(forecastdate) from T_ForecastSite";
            DataTable dtMaxTime = m_Database.GetDataTable(strMaxDateSQL);
            string strMaxDate = "";
            if (dtMaxTime.Rows.Count > 0)
            {
                strMaxDate = dtMaxTime.Rows[0][0].ToString();
            }

            //正式语句
            //string strSQL = String.Format("select Site,AQI from T_ForecastSite , (select MAX(ForecastDate) maxtime from T_ForecastSite)  maxDate WHERE ForecastDate = maxDate.maxtime AND Site in ('58367','58361','58365','58362','58462','58460','58461','58370','58463','58366') AND durationID=7 AND ITEMID=1 AND LST='{0}'", forecastDateTime);
            string strSQL = String.Format("select Site,AQI from T_ForecastSite  WHERE ForecastDate ='" + strMaxDate + "' AND Site in ('58367','58361','58365','58362','58462','58460','58461','58370','58463','58366') AND durationID=7 AND ITEMID=1 AND LST='{0}'", forecastDateTime);

            //测试语句
            //string strSQL = String.Format("select * from AQITest");
            StringBuilder sb = new StringBuilder("{");
            DataSet ds = m_Database.GetDataset(strSQL);

            string style = "";
            string json = "";
            //DataTable dTable = m_Database.GetDataTable(strSQL);
            if (ds.Tables.Count > 0)
            {
                string strSingle = "";
                for (int i = 0; i < ds.Tables.Count; i++)
                {
                    DataTable dTable = ds.Tables[i];

                    //生成实况，综合预报，模式数据的json
                    if (dTable.Rows.Count > 0)
                    {
                        for (int j = 0; j < dTable.Rows.Count; j++)
                        {
                            //创建json，便于前台赋值
                            strSingle = "\"" + dTable.Rows[j]["Site"].ToString() + "\":\"" + dTable.Rows[j]["AQI"].ToString() + "\",";
                            sb.Append(strSingle);
                        }

                    }
                }

            }
            //sb.Append(string.Format("nowDateTime:'{0}'", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            if (sb.Length > 1)
            {
                sb.Remove(sb.Length - 1, 1);
                sb.Append("}");
            }
            else
                sb.Length = 0;

            return sb.ToString();
        }

        //获得空气污染气象条件图片
        public string GetRegionAirPollutionImg(string releaseTime)
        {
            string strPeriod = "024";
            if (releaseTime == "7")
            {
                strPeriod = "024";
            }
            else if (releaseTime == "17")
            {
                strPeriod = "048";
            }
            DateTime dtNow = DateTime.Now;
            string strForeDate = dtNow.AddDays(-1).ToString("yyyy-MM-dd 20:00:00");
            string strSQL = "SELECT ('Product/'+Folder + '/' + Name) AS DM FROM dbo.T_LQS WHERE ForecastDate='" + strForeDate + "' and Period='" + strPeriod + "'";
            //测试用
            //string strSQL = "SELECT ('Product/'+Folder + '/' + Name) AS DM FROM dbo.T_LQS WHERE ForecastDate='" + strForeDate + "' and Period='048'";           
            DataSet ds = m_Database.GetDataset(strSQL);
            if (ds.Tables.Count > 0)
            {
                DataTable dtElement = ds.Tables[0];
                if (dtElement.Rows.Count > 0)
                {
                    return dtElement.Rows[0][0].ToString();
                }
            }
            return "";
        }

        //将日期字符串转为存到数据库当中的格式
        private string ConvertDatetime(string inputDate)
        {
            string strNewDate = "";
            try
            {
                if (inputDate != null && inputDate != "")
                {

                    strNewDate += inputDate.Split('年', '月', '日')[0];
                    int year = Convert.ToInt32(inputDate.Split('年', '月', '日')[0]);
                    int month = Convert.ToInt32(inputDate.Split('年', '月', '日')[1]);
                    int day = Convert.ToInt32(inputDate.Split('年', '月', '日')[2]);
                    DateTime date = new DateTime(year, month, day);

                    strNewDate = date.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
            catch { }
            return strNewDate;
        }

        private DateTime GetDatetime(string inputDate)
        {
            string strNewDate = "";
            DateTime date = DateTime.Now;
            try
            {
                if (inputDate != null && inputDate != "")
                {

                    strNewDate += inputDate.Split('年', '月', '日')[0];
                    int year = Convert.ToInt32(inputDate.Split('年', '月', '日')[0]);
                    int month = Convert.ToInt32(inputDate.Split('年', '月', '日')[1]);
                    int day = Convert.ToInt32(inputDate.Split('年', '月', '日')[2]);
                    date = new DateTime(year, month, day);

                    strNewDate = date.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
            catch { }
            return date;
        }

        //填充模板生成新的word文档
        public void CreateWordFromModel(string modelPath, DataTable inputTable, string newPath)
        {
            WordHelper wordHelper = new WordHelper(modelPath);
            Table templateTable = (Table)wordHelper.Document.GetChild(NodeType.Table, 0, true);
            string strBookMark = wordHelper.Document.Range.Bookmarks[0].ToString();
            //Row clonedRow = (Row)templateTable.LastRow.Clone(true);
            //templateTable.RemoveAllChildren();
            string strBookmark = "";
            string strFillValue = "";
            //书签前缀标志
            string strBookPrefix = "";
            for (int i = 1; i < 11; i++)
            {
                for (int j = 0; j < inputTable.Columns.Count; j++)
                {
                    if (j == 0)
                    {
                        strBookPrefix = "WRLevel";
                    }
                    else if (j == 1)
                    {
                        strBookPrefix = "PP";
                    }
                    else if (j == 2)
                    {
                        strBookPrefix = "AQI";
                    }
                    else if (j == 3)
                    {
                        strBookPrefix = "HazeLevel";
                    }
                    strBookmark = strBookPrefix + i.ToString();
                    strFillValue = inputTable.Rows[i - 1][j].ToString();
                    wordHelper.Replace(strBookmark, strFillValue);
                }
            }
            Table table = wordHelper.GetTable(0);
            wordHelper.SaveAs(newPath, Aspose.Words.SaveFormat.Doc);
        }

        public string GetAQIAreaReportTextTest(string foracastDate)
        {
            string strContent = "";
            DateTime dtNow = DateTime.Now.Date;
            dtNow = dtNow.AddDays(1);
            //if (forecastDate != "")
            //    dtNow = DateTime.Parse(forecastDate);
            string forecastDateTime = dtNow.ToString("yyyy-MM-dd 20:00:00");

            string strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate=(select MAX(ForecastDate) from dbo.T_ForecastSite) AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('7','3','2','6','4','1') AND Site='58637' AND durationID='10' order by Interval asc";
            //string strSQL = "SELECT * FROM dbo.T_AQIResult WHERE durationID='10'";
            StringBuilder sb = new StringBuilder("{");
            DataTable dt = m_Database.GetDataTable(strSQL);
            string strAQISQL = "Select MAX(AQI) FROM (SELECT * FROM dbo.T_ForecastSite WHERE Interval='24' AND durationID='7' AND ForecastDate='2015-12-27 20:00:00.000' AND Site='58637') SiteAQI";

            string s = "Select MAX(AQI) FROM (SELECT * FROM dbo.T_ForecastSite WHERE Interval='24' AND durationID='7' AND ForecastDate='2015-12-23 20:00:00.000' AND Site='58637') SiteAQI, (select * from dbo.T_ForecastSite WHERE Interval='24' AND durationID='7' AND ForecastDate='2015-12-23 20:00:00.000' AND Site='58637') m";

            DataTable dtAQI = m_Database.GetDataTable(strAQISQL);
            string strAQI = "56";
            string strAQIItem = "2";



            //根据站点编号，日期和24小时AQI预报值，生成预报文本
            strContent = GetReportText(dt, "58637", strAQI, strAQIItem, "0", "0");
            return strContent;
        }

        public string GetAQIAreaReportText(string siteID, string maxDate)
        {
            string strContent = "";
            DateTime dtNow = DateTime.Now.Date;
            dtNow = dtNow.AddDays(1);
            string forecastDateTime = dtNow.ToString("yyyy-MM-dd 20:00:00");
            string strSQL = "";
            if (maxDate == "")
            {
                //strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='"+forecastDateTime+"' from dbo.T_ForecastSite) AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('7','3','2','6','4','1') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
                strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + forecastDateTime + "' from dbo.T_ForecastSite) AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('1','2','3','4','5','6') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
            }
            else
            {
                //strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + maxDate + "' AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('7','3','2','6','4','1') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
                strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + maxDate + "' AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('1','2','3','4','5','6') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
            }

            //string strSQL ="SELECT * FROM dbo.T_AreaResult where Site='58606'";
            DataTable dt = m_Database.GetDataTable(strSQL);
            string strAQI = "0";
            string strAQIItem = "0";

            DataTable dtAQI = GetReportTextAQIValueAndItemIDTable(forecastDateTime, siteID);
            if (dtAQI.Rows.Count > 0)
            {
                strAQI = dtAQI.Rows[0]["AQI"].ToString();
                strAQIItem = dtAQI.Rows[0]["ITEMID"].ToString();
            }

            //查找对应站点的经纬度
            string strCordSQL = "SELECT * FROM dbo.sta_reg_set WHERE station_co='" + siteID + "'";
            DataTable dtXY = m_Database.GetDataTable(strCordSQL);
            //纬度
            string strX = "";
            //经度
            string strY = "";
            if (dtXY.Rows.Count > 0)
            {
                double dblX = Convert.ToDouble(dtXY.Rows[0]["x"].ToString());
                double dblY = Convert.ToDouble(dtXY.Rows[0]["y"].ToString());
                Math.Round(dblX, 2, MidpointRounding.AwayFromZero);
                strX = (Math.Round(dblX, 2, MidpointRounding.AwayFromZero) * 100).ToString();
                strY = (Math.Round(dblY, 2, MidpointRounding.AwayFromZero) * 100).ToString();

            }
            //根据站点编号，日期和24小时AQI预报值，生成预报文本
            strContent = GetReportText(dt, siteID, strAQI, strAQIItem, strX, strY);
            return strContent;
        }

        public string GetAQIAreaReportTextNew(string siteID, string maxDate)
        {
            string strContent = "";
            DateTime dtNow = DateTime.Now.Date;
            //dtNow = dtNow.AddDays(1);
            string forecastDateTime = dtNow.ToString("yyyy-MM-dd 20:00:00");
            //maxDate = "2016-03-16 20:00:00.000";
            int intMark = DateTime.Now.Day - Convert.ToDateTime(maxDate).Day;
            string strSQL = "";
            //strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + forecastDateTime + "' from dbo.T_ForecastSite) AND Interval in ('";
            strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + maxDate + "' AND Interval in ('";
            strSQL += (24 * (Math.Abs(intMark)) + 3).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 6).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 9).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 12).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 15).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 18).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 21).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 24).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 30).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 36).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 42).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 48).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 54).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 60).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 66).ToString() + "','";
            strSQL += (24 * (Math.Abs(intMark)) + 72).ToString() + "')";
            strSQL += " AND ITEMID in('1','2','3','4','5','6') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
           
                
            //if (maxDate == "")
            //{
            //    //strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='"+forecastDateTime+"' from dbo.T_ForecastSite) AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('7','3','2','6','4','1') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
            //    strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + forecastDateTime + "' from dbo.T_ForecastSite) AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('1','2','3','4','5','6') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
            //}
            //else
            //{
            //    //strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + maxDate + "' AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('7','3','2','6','4','1') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
            //    strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + maxDate + "' AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('1','2','3','4','5','6') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
            //}

            //string strSQL ="SELECT * FROM dbo.T_AreaResult where Site='58606'";
            DataTable dt = m_Database.GetDataTable(strSQL);
            string strAQI = "0";
            string strAQIItem = "0";

            DataTable dtAQI = GetReportTextAQIValueAndItemIDTableNew(forecastDateTime,maxDate, siteID);
            if (dtAQI.Rows.Count > 0)
            {
                strAQI = dtAQI.Rows[0]["AQI"].ToString();
                strAQIItem = dtAQI.Rows[0]["ITEMID"].ToString();
            }

            //查找对应站点的经纬度
            string strCordSQL = "SELECT * FROM dbo.sta_reg_set WHERE station_co='" + siteID + "'";
            DataTable dtXY = m_Database.GetDataTable(strCordSQL);
            //纬度
            string strX = "";
            //经度
            string strY = "";
            if (dtXY.Rows.Count > 0)
            {
                double dblX = Convert.ToDouble(dtXY.Rows[0]["x"].ToString());
                double dblY = Convert.ToDouble(dtXY.Rows[0]["y"].ToString());
                Math.Round(dblX, 2, MidpointRounding.AwayFromZero);
                strX = (Math.Round(dblX, 2, MidpointRounding.AwayFromZero) * 100).ToString();
                strY = (Math.Round(dblY, 2, MidpointRounding.AwayFromZero) * 100).ToString();

            }
            //根据站点编号，日期和24小时AQI预报值，生成预报文本
            strContent = GetReportText(dt, siteID, strAQI, strAQIItem, strX, strY);
            return strContent;
        }
        //2015年12月3日，从数据接口读取数据
        public string ReadFromDataInterfacePast(string forecastDate)
        {
            string strModuleName = "ManualCenter,SMCSubmit,ManualSubmit";
            string strDatsaUrl = ConfigurationManager.AppSettings["AQIPeriodDataURL"];
            strDatsaUrl += "&method=forecastGroup&module=";
            strDatsaUrl += strModuleName;
            strDatsaUrl += "&forecastDate=";

            DateTime dtNow = DateTime.Now;
            string strforeDate = dtNow.ToString("yyyy-MM-dd 18:00:00");
            string strDateString = strforeDate;
            //标记获取的数据日期，如果今天还没获取到自动去获取昨天的
            string strDateSym = "Today";
            //返回到前台的字符串
            StringBuilder sb = new StringBuilder();
            try
            {
                WebRequest request = WebRequest.Create(strDatsaUrl + strDateString);
                WebResponse response = request.GetResponse();
                StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("gb2312"));
                string strMsg = reader.ReadToEnd();
                reader.Close();
                reader.Dispose();
                JsonDeserial jsonDex = new JsonDeserial();
                //将json解析为集合
                List<AQIPeriodCell> cellList = jsonDex.GetAQICellsFromJson(strMsg);
                //今天数据为空
                if (cellList.Count < 2)
                {
                    strDateString = dtNow.AddDays(-1).ToString("yyyy-MM-dd 18:00:00");
                    strDateSym = "Yesterday";
                    request = WebRequest.Create(strDatsaUrl + strDateString);
                    response = request.GetResponse();
                    reader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("gb2312"));
                    strMsg = reader.ReadToEnd();
                    reader.Close();
                    reader.Dispose();
                    //将json解析为集合
                    cellList = jsonDex.GetAQICellsFromJson(strMsg);
                }

                //筛选用于界面展示的数据
                #region 当天的数据
                //编号前缀，P表示气象局或环境监测中心，H表示综合数据
                string strPrefix = "1";
                string strPeriodID = "3";
                string strOraID = "1";
                string strDuraID = "1";
                string strItemID = "1";
                //实测浓度值
                string strDenValue = "0";
                //AQI指数
                string strAQIValue = "0";
                for (int i = 0; i < cellList.Count; i++)
                {
                    //判断时间间隔
                    if (cellList[i].Period == "24")
                    {
                        strPeriodID = "3";
                    }
                    else if (cellList[i].Period == "48")
                    {
                        strPeriodID = "4";
                    }

                    //判断发布机构

                    //ManualCenter 指的是总的
                    if (cellList[i].Organization == "ManualCenter")
                    {
                        strPrefix = "H";
                        strOraID = "1";
                    }
                    //SMCSubmit 气象局填写
                    else if (cellList[i].Organization == "SMCSubmit")
                    {
                        strPrefix = "P";
                        strOraID = "2";
                    }
                    //环境监测中心
                    else if (cellList[i].Organization == "ManualSubmit")
                    {
                        strPrefix = "P";
                        strOraID = "1";
                    }

                    //判断时段
                    strDuraID = cellList[i].Duration;

                    //判断AQIItem类别
                    strItemID = cellList[i].AQIId;
                    strDenValue = cellList[i].Value;
                    strAQIValue = cellList[i].AQI;
                    sb.Append("\"" + strPrefix + strPeriodID + strOraID + strDuraID + strItemID + "\"");
                    strAQIValue = strAQIValue == "null" ? "" : strAQIValue;
                    strDenValue = strDenValue == "null" ? "" : strDenValue;
                    sb.Append(":" + "\"" + strDenValue + "/" + "<span>" + strAQIValue + "</span>" + "\",");
                }

                #endregion


                return strDateSym + "&{" + sb.ToString() + "}";
            }
            catch { }
            string strJson = "";
            return strJson;
        }

        public string ReadFromDataInterface(string forecastDate)
        {
            string strModuleName = "ManualCenter,SMCSubmit,ManualSubmit";
            string strDatsaUrl = ConfigurationManager.AppSettings["AQIPeriodDataURL"];
            strDatsaUrl += "&method=forecastGroup&module=";
            strDatsaUrl += strModuleName;
            strDatsaUrl += "&forecastDate=";

            DateTime dtNow = DateTime.Now;
            string strforeDate = dtNow.ToString("yyyy-MM-dd 18:00:00");
            string strDateString = strforeDate;
            //标记获取的数据日期，如果今天还没获取到自动去获取昨天的
            string strDateSym = "Today";
            //返回到前台的字符串
            StringBuilder sb = new StringBuilder();
            try
            {
                WebRequest request = WebRequest.Create(strDatsaUrl + strDateString);
                WebResponse response = request.GetResponse();
                StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("gb2312"));
                string strMsg = reader.ReadToEnd();
                reader.Close();
                reader.Dispose();
                JsonDeserial jsonDex = new JsonDeserial();
                //将json解析为集合
                List<AQIPeriodCell> cellList = jsonDex.GetAQICellsFromJson(strMsg);
                //今天数据为空
                if (cellList.Count < 2)
                {
                    strDateString = dtNow.AddDays(-1).ToString("yyyy-MM-dd 18:00:00");
                    strDateSym = "Yesterday";
                    request = WebRequest.Create(strDatsaUrl + strDateString);
                    response = request.GetResponse();
                    reader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("gb2312"));
                    strMsg = reader.ReadToEnd();
                    reader.Close();
                    reader.Dispose();
                    //将json解析为集合

                    cellList = jsonDex.GetAQICellsFromJson(strMsg);
                }

                //筛选用于界面展示的数据
                #region 当天的数据
                //编号前缀，P表示气象局或环境监测中心，H表示综合数据
                string strPrefix = "1";
                string strPeriodID = "3";
                string strOraID = "1";
                string strDuraID = "1";
                string strItemID = "1";
                //实测浓度值
                string strDenValue = "0";
                //AQI指数
                string strAQIValue = "0";
                //起报时间
                DateTime dtForecast;
                //预报时间
                DateTime dtLST;
                //起报与预报的间隔
                int intIntervalDays;
                for (int i = 0; i < cellList.Count; i++)
                {

                    if (cellList[i].ForecastDate != null && cellList[i].LST != null && cellList[i].ForecastDate != "" && cellList[i].LST != "")
                    {
                        dtForecast = DateTime.Parse(cellList[i].ForecastDate + ":00:00");
                        dtLST = DateTime.Parse(cellList[i].LST + ":00:00");
                        //intIntervalDays = dtForecast.Day - dtLST.Day;
                        DateTime dtForecastShort = Convert.ToDateTime(dtForecast.ToShortDateString());
                        DateTime dtLSTShort = Convert.ToDateTime(dtLST.ToShortDateString());
                        intIntervalDays = (dtForecastShort - dtLSTShort).Days;
                        //判断时间间隔
                        strPeriodID = (2 - (intIntervalDays - 1)).ToString();
                    }

                    //if (cellList[i].Period == "24")
                    //{
                    //    strPeriodID = "3";
                    //}
                    //else if (cellList[i].Period == "48")
                    //{
                    //    strPeriodID = "4";
                    //}

                    //判断发布机构

                    //ManualCenter 指的是总的
                    if (cellList[i].Organization == "ManualCenter")
                    {
                        strPrefix = "H";
                        strOraID = "1";
                    }
                    //SMCSubmit 气象局填写
                    else if (cellList[i].Organization == "SMCSubmit")
                    {
                        strPrefix = "P";
                        strOraID = "2";
                    }
                    //环境监测中心
                    else if (cellList[i].Organization == "ManualSubmit")
                    {
                        strPrefix = "P";
                        strOraID = "1";
                    }

                    //判断时段
                    strDuraID = cellList[i].Duration;

                    //判断AQIItem类别
                    strItemID = cellList[i].AQIId;
                    string strUseItem = "";
                    if (Convert.ToInt32(strItemID) > 0)
                    {
                        strUseItem = strItemID;
                        strDenValue = cellList[i].Value;
                        strAQIValue = cellList[i].AQI;
                        sb.Append("\"" + strPrefix + strPeriodID + strOraID + strDuraID + strUseItem + "\"");
                        strAQIValue = strAQIValue == "null" ? "" : strAQIValue;
                        strDenValue = strDenValue == "null" ? "" : strDenValue;
                        sb.Append(":" + "\"" + strDenValue + "/" + "<span>" + strAQIValue + "</span>" + "\",");
                    }
                    //界面表格最后两列的AQI 
                    else if (Convert.ToInt32(strItemID) == 0)
                    {
                        strUseItem = "6";
                        strDenValue = cellList[i].Value;
                        strAQIValue = cellList[i].AQI;
                        string strFirstItem = cellList[i].Parameter;
                        sb.Append("\"" + strPrefix + strPeriodID + strOraID + strDuraID + strUseItem + "\"");
                        strAQIValue = strAQIValue == "null" ? "" : strAQIValue;
                        strDenValue = strDenValue == "null" ? "" : strDenValue;
                        sb.Append(":" + "\"" + strAQIValue + "/" + "<span>" + strFirstItem + "</span>" + "\",");
                    }


                }
                #endregion
                return strDateSym + "&{" + sb.ToString() + "}";
            }
            catch (Exception e) { }
            string strJson = "";
            return strJson;
        }

        //读取前一天的AQI分时段数据
        public string ReadFromDataInterfaceHistory()
        {
            string strModuleName = "ManualCenter,SMCSubmit,ManualSubmit";
            string strDatsaUrl = ConfigurationManager.AppSettings["AQIPeriodDataURL"];
            strDatsaUrl += "&method=forecastGroup&module=";
            strDatsaUrl += strModuleName;
            strDatsaUrl += "&forecastDate=";

            DateTime dtNow = DateTime.Now;
            string strforeDate = dtNow.AddDays(-1).ToString("yyyy-MM-dd 18:00:00");
            string strDateString = strforeDate;
            //标记获取的数据日期，如果昨天还没获取到自动去获取前天的
            string strDateSym = "Yesterday";
            //返回到前台的字符串
            StringBuilder sb = new StringBuilder();
            try
            {
                WebRequest request = WebRequest.Create(strDatsaUrl + strDateString);
                WebResponse response = request.GetResponse();
                StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("gb2312"));
                string strMsg = reader.ReadToEnd();
                reader.Close();
                reader.Dispose();
                JsonDeserial jsonDex = new JsonDeserial();
                //将json解析为集合
                List<AQIPeriodCell> cellList = jsonDex.GetAQICellsFromJson(strMsg);
                //今天数据为空
                if (cellList.Count < 2)
                {
                    strDateString = dtNow.AddDays(-2).ToString("yyyy-MM-dd 18:00:00");
                    strDateSym = "Before";
                    request = WebRequest.Create(strDatsaUrl + strDateString);
                    response = request.GetResponse();
                    reader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("gb2312"));
                    strMsg = reader.ReadToEnd();
                    reader.Close();
                    reader.Dispose();
                    //将json解析为集合

                    cellList = jsonDex.GetAQICellsFromJson(strMsg);
                }

                //筛选用于界面展示的数据
                #region 当天的数据
                //编号前缀，P表示气象局或环境监测中心，H表示综合数据
                string strPrefix = "1";
                string strPeriodID = "3";
                string strOraID = "1";
                string strDuraID = "1";
                string strItemID = "1";
                //实测浓度值
                string strDenValue = "0";
                //AQI指数
                string strAQIValue = "0";
                //起报时间
                DateTime dtForecast;
                //预报时间
                DateTime dtLST;
                //起报与预报的间隔
                int intIntervalDays;
                for (int i = 0; i < cellList.Count; i++)
                {

                    if (cellList[i].ForecastDate != null && cellList[i].LST != null && cellList[i].ForecastDate != "" && cellList[i].LST != "")
                    {
                        dtForecast = DateTime.Parse(cellList[i].ForecastDate + ":00:00");
                        dtLST = DateTime.Parse(cellList[i].LST + ":00:00");
                        //intIntervalDays = dtForecast.Day - dtLST.Day;
                        DateTime dtForecastShort = Convert.ToDateTime(dtForecast.ToShortDateString());
                        DateTime dtLSTShort = Convert.ToDateTime(dtLST.ToShortDateString());
                        intIntervalDays = (dtForecastShort - dtLSTShort).Days;

                        //判断时间间隔
                        strPeriodID = (2 - (intIntervalDays - 1)).ToString();
                    }

                    //if (cellList[i].Period == "24")
                    //{
                    //    strPeriodID = "3";
                    //}
                    //else if (cellList[i].Period == "48")
                    //{
                    //    strPeriodID = "4";
                    //}

                    //判断发布机构

                    //ManualCenter 指的是总的
                    if (cellList[i].Organization == "ManualCenter")
                    {
                        strPrefix = "H";
                        strOraID = "1";
                    }
                    //SMCSubmit 气象局填写
                    else if (cellList[i].Organization == "SMCSubmit")
                    {
                        strPrefix = "P";
                        strOraID = "2";
                    }
                    //环境监测中心
                    else if (cellList[i].Organization == "ManualSubmit")
                    {
                        strPrefix = "P";
                        strOraID = "1";
                    }

                    //判断时段
                    strDuraID = cellList[i].Duration;

                    //判断AQIItem类别
                    strItemID = cellList[i].AQIId;
                    string strUseItem = "";
                    if (Convert.ToInt32(strItemID) > 0)
                    {
                        strUseItem = strItemID;
                        strDenValue = cellList[i].Value;
                        strAQIValue = cellList[i].AQI;
                        sb.Append("\"" + strPrefix + strPeriodID + strOraID + strDuraID + strUseItem + "\"");
                        strAQIValue = strAQIValue == "null" ? "" : strAQIValue;
                        strDenValue = strDenValue == "null" ? "" : strDenValue;
                        sb.Append(":" + "\"" + strDenValue + "/" + "<span>" + strAQIValue + "</span>" + "\",");
                    }
                    //界面表格最后两列的AQI 
                    else if (Convert.ToInt32(strItemID) == 0)
                    {
                        strUseItem = "6";
                        strDenValue = cellList[i].Value;
                        strAQIValue = cellList[i].AQI;
                        string strFirstItem = cellList[i].Parameter;
                        sb.Append("\"" + strPrefix + strPeriodID + strOraID + strDuraID + strUseItem + "\"");
                        strAQIValue = strAQIValue == "null" ? "" : strAQIValue;
                        strDenValue = strDenValue == "null" ? "" : strDenValue;
                        sb.Append(":" + "\"" + strAQIValue + "/" + "<span>" + strFirstItem + "</span>" + "\",");
                    }


                }
                #endregion
                return strDateSym + "&{" + sb.ToString() + "}";
            }
            catch (Exception e) { }
            string strJson = "";
            return strJson;
        }


        //2015年12月4日，AQI分时段数据入库        
        public string SaveAQIPeriodDataCopy(string forecastDate, string data, string functionName, string ftpString, string txtContent, string msgContent, string fileTxtName, string fileMsgName, string userName)
        {

            string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
            string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");

            DateTime dtNow = DateTime.Now;
            string strForecastDate = dtNow.ToString("yyyy-MM-dd 00:00:00");
            if (IsPublished("AQIPeriod", strRcdTime) == true)
            {
                return "published";
            }
            string strModule = "null";
            string strPeriod = "null";
            //预报时效
            string strLST = "null";
            string strDurID = "null";
            string strAQIItemId;
            //AQI指数值
            string srAQIValue = "null";
            //实测浓度值
            string srDen = "";

            StringBuilder sb = new StringBuilder("INSERT INTO T_ForecastGroup (LST, ForecastDate, Interval,PERIOD,Site,durationID,ITEMID,Value,AQI,Module,Quality,Parameter,UserName)");
            if (data != "")
            {
                data = data.TrimEnd(',');
                string[] cells = data.Split(',');
                string strCellValue = "";
                for (int i = 0; i < cells.Length; i++)
                {
                    //单元格id转为字符数组
                    char[] idArray = cells[i].Split(':')[0].ToCharArray();
                    //前端界面单元格内值（45.0/63）
                    strCellValue = cells[i].Split(':')[1];
                    //气象局
                    if (idArray[0] == 'P' && idArray[2] == '1')
                    {
                        strModule = "WRF";
                    }
                    //环境中心
                    else if (idArray[0] == 'P' && idArray[2] == '2')
                    {
                        strModule = "ENV";
                    }
                    //环境中心
                    else if (idArray[0] == 'H')
                    {
                        strModule = "GENERAL";
                    }

                    //判断时间间隔（24小时，48小时）

                    if (idArray[1] == '3' || (idArray[1] == '4' && idArray[3] == '1'))
                    {
                        strPeriod = "24";
                        strLST = dtNow.ToString("yyyy-MM-dd 00:00:00");
                    }
                    else if (idArray[1] == '4' && idArray[3] != '1')
                    {
                        strPeriod = "24";
                        strLST = dtNow.AddHours(24).ToString("yyyy-MM-dd 00:00:00");
                    }
                    else if (idArray[1] == '5')
                    {
                        strPeriod = "48";
                        strLST = dtNow.AddHours(48).ToString("yyyy-MM-dd 00:00:00");
                    }

                    strDurID = idArray[3].ToString();
                    strAQIItemId = idArray[4].ToString();
                    int intAQIItemID = Convert.ToInt32(strAQIItemId);
                    //五种污染物的数据
                    if (intAQIItemID > 0 && intAQIItemID < 6)
                    {
                        srAQIValue = strCellValue.Split('/')[1] != "" ? strCellValue.Split('/')[1] : "null";
                        srDen = strCellValue.Split('/')[0] != "" ? strCellValue.Split('/')[0] : "null";
                        if (i == 0)
                        {
                            sb.Append(string.Format(" SELECT '{0}','{1}',{2},{3},'{4}',{5},{6},{7},{8},'{9}','{10}','{11}','{12}'", strLST, strForecastDate, "0", strPeriod, "58637", strDurID, strAQIItemId, srDen, srAQIValue, strModule, "", "", userName));
                        }
                        else
                        {
                            sb.Append(string.Format(" UNION ALL SELECT  '{0}','{1}',{2},{3},'{4}',{5},{6},{7},{8},'{9}','{10}','{11}','{12}'", strLST, strForecastDate, "0", strPeriod, "58637", strDurID, strAQIItemId, srDen, srAQIValue, strModule, "", "", userName));
                        }
                    }
                    //AQI的数据
                    else
                    {
                        srAQIValue = strCellValue.Split('/')[0] != "" ? strCellValue.Split('/')[0] : "null";
                        sb.Append(string.Format(" UNION ALL SELECT  '{0}','{1}',{2},{3},'{4}',{5},{6},{7},{8},'{9}','{10}','{11}','{12}'", strLST, strForecastDate, "0", strPeriod, "58637", strDurID, strAQIItemId, "null", srAQIValue, strModule, "", "", userName));
                    }
                }
                string strSQL = sb.ToString();
                m_Database.Execute("delete from T_ForecastGroup where ForecastDate='" + strForecastDate + "'");
                m_Database.Execute(strSQL);


                InsertIntoStateTable("AQIPeriod", strRcdTime, strDeadLineTime, "1", "2");
                //将AQI分时段文本和短信内容保存到服务器固定路径，发布时进行读取
                SaveAQIPeriodTextAngMsg(txtContent, msgContent);
                return "success";
            }
            return "fail";
        }

        public string SaveAQIPeriodData(string forecastDate, string data, string functionName, string ftpString, string txtContent, string msgContent, string fileTxtName, string fileMsgName, string userName)
        {

            string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
            string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");

            DateTime dtNow = DateTime.Now;
            string strForecastDate = dtNow.ToString("yyyy-MM-dd 00:00:00");
            if (IsPublished("AQIPeriod", strRcdTime) == true)
            {
                return "published";
            }
            string strModule = "null";
            string strPeriod = "null";
            //预报时效
            string strLST = "null";
            string strDurID = "null";
            string strAQIItemId;
            //AQI指数值
            string srAQIValue = "null";
            //实测浓度值
            string srDen = "";

            StringBuilder sb = new StringBuilder("INSERT INTO T_ForecastGroup (LST, ForecastDate, Interval,PERIOD,Site,durationID,ITEMID,Value,AQI,Module,Quality,Parameter,UserName)");
            if (data != "")
            {
                data = data.TrimEnd(',');
                string[] cells = data.Split(',');
                string strCellValue = "";
                for (int i = 0; i < cells.Length; i++)
                {
                    //单元格id转为字符数组
                    char[] idArray = cells[i].Split(':')[0].ToCharArray();
                    //前端界面单元格内值（45.0/63）
                    strCellValue = cells[i].Split(':')[1];
                    //气象局
                    if (idArray[0] == 'P' && idArray[2] == '1')
                    {
                        strModule = "WRF";
                    }
                    //环境中心
                    else if (idArray[0] == 'P' && idArray[2] == '2')
                    {
                        strModule = "ENV";
                    }
                    //环境中心
                    else if (idArray[0] == 'H')
                    {
                        strModule = "GENERAL";
                    }

                    //判断时间间隔（24小时，48小时）

                    if (idArray[1] == '3' || (idArray[1] == '4' && idArray[3] == '1'))
                    {
                        strPeriod = "24";
                        strLST = dtNow.ToString("yyyy-MM-dd 00:00:00");
                    }
                    else if (idArray[1] == '4' && idArray[3] != '1')
                    {
                        strPeriod = "24";
                        strLST = dtNow.AddHours(24).ToString("yyyy-MM-dd 00:00:00");
                    }
                    else if (idArray[1] == '5')
                    {
                        strPeriod = "48";
                        strLST = dtNow.AddHours(48).ToString("yyyy-MM-dd 00:00:00");
                    }

                    strDurID = idArray[3].ToString();
                    strAQIItemId = idArray[4].ToString();
                    int intAQIItemID = Convert.ToInt32(strAQIItemId);
                    //五种污染物的数据
                    if (intAQIItemID > 0 && intAQIItemID < 6)
                    {
                        srAQIValue = strCellValue.Split('/')[1] != "" ? strCellValue.Split('/')[1] : "null";
                        srDen = strCellValue.Split('/')[0] != "" ? strCellValue.Split('/')[0] : "null";
                        if (i == 0)
                        {
                            sb.Append(string.Format(" SELECT '{0}','{1}',{2},{3},'{4}',{5},{6},{7},{8},'{9}','{10}','{11}','{12}'", strLST, strForecastDate, "0", strPeriod, "58637", strDurID, strAQIItemId, srDen, srAQIValue, strModule, "", "", userName));
                        }
                        else
                        {
                            sb.Append(string.Format(" UNION ALL SELECT  '{0}','{1}',{2},{3},'{4}',{5},{6},{7},{8},'{9}','{10}','{11}','{12}'", strLST, strForecastDate, "0", strPeriod, "58637", strDurID, strAQIItemId, srDen, srAQIValue, strModule, "", "", userName));
                        }
                    }
                    //AQI的数据
                    else
                    {
                        //srAQIValue = strCellValue.Split('/')[0] != "" ? strCellValue.Split('/')[0] : "null";
                        //sb.Append(string.Format(" UNION ALL SELECT  '{0}','{1}',{2},{3},'{4}',{5},{6},{7},{8},'{9}','{10}','{11}','{12}'", strLST, strForecastDate, "0", strPeriod, "58637", strDurID, strAQIItemId, "null", srAQIValue, strModule, "", "", userName));
                        srAQIValue = strCellValue.Split('/')[0] != "" ? strCellValue.Split('/')[0] : "null";
                        string strFirstItem = strCellValue.Split('/')[1] != "" ? strCellValue.Split('/')[1] : "";
                        sb.Append(string.Format(" UNION ALL SELECT  '{0}','{1}',{2},{3},'{4}',{5},{6},{7},{8},'{9}','{10}','{11}','{12}'", strLST, strForecastDate, "0", strPeriod, "58637", strDurID, "0", "null", srAQIValue, strModule, "", strFirstItem, userName));
                    }
                }
                string strSQL = sb.ToString();
                m_Database.Execute("delete from T_ForecastGroup where ForecastDate='" + strForecastDate + "'");
                m_Database.Execute(strSQL);


                InsertIntoStateTable("AQIPeriod", strRcdTime, strDeadLineTime, "1", "2");
                //将AQI分时段文本和短信内容保存到服务器固定路径，发布时进行读取
                SaveAQIPeriodTextAngMsg(txtContent, msgContent);
                return "success";
            }
            return "fail";
        }

        public string UpLoadAQIPeriod()
        {
            return "success";
        }

        //2015年12月21日，保存AQI分区数据入库
        public string SaveAQIAreaData(string data, string period, string duratonId)
        {

            string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");
            string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 19:30:00.000");

            duratonId = "7";
            //string strMaxForeDate = GetMaxForecastDate("T_ForecastSite");
            string strMaxForeDate = DateTime.Now.ToString("yyyy-MM-dd 00:00:00.000");
            if (IsPublished("AQIArea", strRcdTime) == true)
            {
                return "published";
            }
            StringBuilder sb = new StringBuilder("INSERT INTO T_AQIArea (area, ForecastDate, period,durationID,grade,AQI,itemid,haze)");
            if (data != "")
            {
                string[] cells = data.Split('&');
                string strAreaName = "";
                string strGrade = "";
                string strAQIItemId = "";
                string strAQI = "";
                string strHaze = "";
                for (int i = 0; i < cells.Length; i++)
                {
                    string[] strSingleArea = cells[i].Split('_');
                    switch (strSingleArea[0])
                    {
                        case "58367":
                            strAreaName = "中心城区";
                            break;
                        case "58370":
                            strAreaName = "浦东新区";
                            break;
                        case "58361":
                            strAreaName = "闵行区";
                            break;
                        case "58362":
                            strAreaName = "宝山区";
                            break;
                        case "58462":
                            strAreaName = "松江区";
                            break;
                        case "58460":
                            strAreaName = "金山区";
                            break;
                        case "58461":
                            strAreaName = "青浦区";
                            break;
                        case "58463":
                            strAreaName = "奉贤区";
                            break;
                        case "58365":
                            strAreaName = "嘉定区";
                            break;
                        case "58366":
                            strAreaName = "崇明县";
                            break;
                        default:
                            strAreaName = "中心城区";
                            break;
                    }
                    //污染等级
                    switch (strSingleArea[1])
                    {
                        case "优":
                            strGrade = "1";
                            break;
                        case "良":
                            strGrade = "2";
                            break;
                        case "轻度污染":
                            strGrade = "3";
                            break;
                        case "中度污染":
                            strGrade = "4";
                            break;
                        case "重度污染":
                            strGrade = "5";
                            break;
                        case "严重污染":
                            strGrade = "6";
                            break;
                        default:
                            strGrade = "1";
                            break;
                    }

                    //首要污染物
                    //switch (strSingleArea[2])
                    //{
                    //    case "PM2.5":
                    //        strAQIItemId = "1";
                    //        break;
                    //    case "PM10":
                    //        strAQIItemId = "2";
                    //        break;
                    //    case "NO2":
                    //        strAQIItemId = "3";
                    //        break;
                    //    case "O3-1小时":
                    //        strAQIItemId = "4";
                    //        break;
                    //    case "O3-8小时":
                    //        strAQIItemId = "5";
                    //        break;
                    //    case "CO":
                    //        strAQIItemId = "6";
                    //        break;
                    //    case "SO2":
                    //        strAQIItemId = "7";
                    //        break;
                    //    default:
                    //        strAQIItemId = "1";
                    //        break;
                    //}
                    switch (strSingleArea[2])
                    {
                        case "PM2.5":
                            strAQIItemId = "6";
                            break;
                        case "PM10":
                            strAQIItemId = "3";
                            break;
                        case "NO2":
                            strAQIItemId = "2";
                            break;
                        case "O3-1小时":
                            strAQIItemId = "5";
                            break;
                        case "CO":
                            strAQIItemId = "4";
                            break;
                        case "SO2":
                            strAQIItemId = "1";
                            break;
                        default:
                            strAQIItemId = "0";
                            break;
                    }
                    strAQI = strSingleArea[3];

                    //霾预报
                    switch (strSingleArea[4])
                    {
                        case "无霾":
                            strHaze = "1";
                            break;
                        case "轻微霾":
                            strHaze = "2";
                            break;
                        case "轻度霾":
                            strHaze = "3";
                            break;
                        case "中度霾":
                            strHaze = "4";
                            break;
                        case "重度霾":
                            strHaze = "5";
                            break;
                        case "严重霾":
                            strHaze = "6";
                            break;
                        default:
                            strHaze = "1";
                            break;
                    }
                    if (i == 0)
                    {
                        sb.Append(string.Format(" SELECT '{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}'", strAreaName, strMaxForeDate, period, duratonId, strGrade, strAQI, strAQIItemId, strHaze));
                    }
                    else
                    {
                        sb.Append(string.Format(" UNION ALL SELECT '{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}'", strAreaName, strMaxForeDate, period, duratonId, strGrade, strAQI, strAQIItemId, strHaze));
                    }
                }
                m_Database.Execute("delete from T_AQIArea where ForecastDate='" + strMaxForeDate + "'");
                m_Database.Execute(sb.ToString());



                InsertIntoStateTable("AQIArea", strRcdTime, strDeadLineTime, "1", "2");
                return "success";
            }
            return "fail";
        }

        //2015年12月5日，上传图片文件
        public string UpLoadImg(string ftpURL, string user, string password, string sourceFileName, string upLoadFileName)
        {
            if (sourceFileName != "")
            {
                sourceFileName = sourceFileName.TrimStart('.', '.');
                sourceFileName = sourceFileName.Replace('/', '\\');
                string strBase = ConfigurationManager.AppSettings["ImgProductBaseURL"].ToString();
                //sourceFileName = sourceFileName.Replace(@"\Product", strBase);
                sourceFileName = sourceFileName.Replace(@"\Product", strBase);
                //string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;                
                //string strImgUrl = strBase + sourceFileName;
                string strImgUrl = sourceFileName;
                string strFTPIPString = null;
                string strFTPUser = null;
                string strFTPPSW = null;
                string strFtpIP = "";

                strFTPIPString = ftpURL;
                strFtpIP = strFTPIPString;
                strFTPUser = user;
                strFTPPSW = password;

                if (sourceFileName == "" || sourceFileName == null)
                {
                    return "fail";
                }
                if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                {
                    if (strFtpIP.IndexOf('/') > 0)
                    {
                        strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                    }
                    //string uri = "ftp://" + strFTPIPString + "/";
                    Ftp ftp = new Ftp(strFTPIPString, strFTPUser, strFTPPSW);
                    ftp.Upload(strImgUrl, upLoadFileName);
                    return "success";
                }
            }
            return "fail";
        }

        public string UpLoadImgExportMapCopy(string ftpURL, string user, string password, string sourceFileName, string upLoadFileName)
        {
            if (sourceFileName != "")
            {
                sourceFileName = sourceFileName.TrimStart('.', '.');
                sourceFileName = sourceFileName.Replace('/', '\\');
                string strBase = ConfigurationManager.AppSettings["ExportMapURL"].ToString();
                sourceFileName = DateTime.Now.AddDays(-1).ToString("yyyyMMdd20") + "_" + DateTime.Now.AddDays(1).ToString("yyyyMMdd08") + "_diffusion_sh_mmdd.GIF";
                sourceFileName = strBase + "\\" + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.ToString("yyyyMMddHH") + "\\" + sourceFileName;
                string strImgUrl = sourceFileName;
                string strFTPIPString = null;
                string strFTPUser = null;
                string strFTPPSW = null;
                string strFtpIP = "";

                strFTPIPString = ftpURL;
                strFtpIP = strFTPIPString;
                strFTPUser = user;
                strFTPPSW = password;

                if (sourceFileName == "" || sourceFileName == null)
                {
                    return "fail";
                }
                if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                {
                    if (strFtpIP.IndexOf('/') > 0)
                    {
                        strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                    }
                    //string uri = "ftp://" + strFTPIPString + "/";
                    Ftp ftp = new Ftp(strFTPIPString, strFTPUser, strFTPPSW);
                    ftp.Upload(strImgUrl, upLoadFileName);
                    return "success";
                }
            }
            return "fail";
        }

        public string UpLoadImgExportMapCopy2(string ftpURL, string user, string password, string hourType)
        {
            string sourceFileName = "YYYYMMDDHH_YyYyMmDdHh_diffusion_sh_mmdd.GIF";
            sourceFileName = sourceFileName.Replace("YYYYMMDDHH", DateTime.Now.AddDays(-1).ToString("yyyyMMdd20"));
            DateTime pubTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 20:00:00"));
            if (hourType == "07")
            {
                sourceFileName = sourceFileName.Replace("YyYyMmDdHh", DateTime.Now.ToString("yyyyMMdd20"));
                pubTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 08:00:00"));
            }
            else if (hourType == "17")
            {
                sourceFileName = sourceFileName.Replace("YyYyMmDdHh", DateTime.Now.AddDays(1).ToString("yyyyMMdd08"));
                pubTime = Convert.ToDateTime(DateTime.Now.AddDays(1).ToString("yyyy-MM-dd 20:00:00"));
            }

            sourceFileName = sourceFileName.Replace("mmdd", DateTime.Now.AddDays(1).ToString("MMdd"));

            string strExMapBaseUrl = ConfigurationManager.AppSettings["ExportMapURL"].ToString();
            string path = string.Format("{0}/{1}/{2}/", strExMapBaseUrl, pubTime.ToString("yyyy"), pubTime.ToString("yyyyMMddHH"));

            string exportImageName = path + sourceFileName;


            if (!File.Exists(exportImageName))
            {
                return "fail";
            }

            else
            {
                //sourceFileName = sourceFileName.TrimStart('.', '.');
                //sourceFileName = sourceFileName.Replace('/', '\\');
                //string strBase = ConfigurationManager.AppSettings["ExportMapURL"].ToString();
                //sourceFileName = DateTime.Now.AddDays(-1).ToString("yyyyMMdd20") + "_" + DateTime.Now.AddDays(1).ToString("yyyyMMdd08") + "_diffusion_sh_mmdd.GIF";
                //sourceFileName = strBase + "\\" + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.ToString("yyyyMMddHH") + "\\" + sourceFileName;

                string strFTPIPString = null;
                string strFTPUser = null;
                string strFTPPSW = null;
                string strFtpIP = "";

                strFTPIPString = ftpURL;
                strFtpIP = strFTPIPString;
                strFTPUser = user;
                strFTPPSW = password;

                //上传的路径
                string upLoadFileName = sourceFileName.Split('\\')[sourceFileName.Split('\\').Length - 1];
                string strImgUrl = exportImageName;
                if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                {
                    if (strFtpIP.IndexOf('/') > 0)
                    {
                        strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                    }
                    //string uri = "ftp://" + strFTPIPString + "/";
                    Ftp ftp = new Ftp(strFTPIPString, strFTPUser, strFTPPSW);
                    ftp.Upload(strImgUrl, upLoadFileName);
                    return "success";
                }
            }
            return "fail";
        }

        public string UpLoadImgExportMap(string ftpURL, string user, string password, string hourType)
        {
            string sourceFileName = "YYYYMMDDHH_YyYyMmDdHh_diffusion_sh_mmdd.GIF";
            //sourceFileName = sourceFileName.Replace("YYYYMMDDHH", DateTime.Now.AddDays(-1).ToString("yyyyMMdd20"));
            DateTime pubTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 20:00:00"));
            if (hourType == "07")
            {
                sourceFileName = sourceFileName.Replace("YYYYMMDDHH", DateTime.Now.AddDays(-2).ToString("yyyyMMdd20"));
                sourceFileName = sourceFileName.Replace("YyYyMmDdHh", DateTime.Now.ToString("yyyyMMdd08"));
                sourceFileName = sourceFileName.Replace("mmdd", DateTime.Now.ToString("MMdd"));
                pubTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 08:00:00"));
            }
            else if (hourType == "17")
            {
                sourceFileName = sourceFileName.Replace("YYYYMMDDHH", DateTime.Now.AddDays(-1).ToString("yyyyMMdd20"));
                sourceFileName = sourceFileName.Replace("YyYyMmDdHh", DateTime.Now.ToString("yyyyMMdd20"));
                sourceFileName = sourceFileName.Replace("mmdd", DateTime.Now.AddDays(1).ToString("MMdd"));
                //pubTime = Convert.ToDateTime(DateTime.Now.AddDays(1).ToString("yyyy-MM-dd 20:00:00"));
                pubTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 20:00:00"));
            }

            //sourceFileName = sourceFileName.Replace("mmdd", DateTime.Now.AddDays(1).ToString("MMdd"));

            string strExMapBaseUrl = ConfigurationManager.AppSettings["ExportMapURL"].ToString();
            string path = string.Format("{0}/{1}/{2}/", strExMapBaseUrl, pubTime.ToString("yyyy"), pubTime.ToString("yyyyMMddHH"));

            string exportImageName = path + sourceFileName;


            if (!File.Exists(exportImageName))
            {
                return "fail";
            }

            else
            {
                //sourceFileName = sourceFileName.TrimStart('.', '.');
                //sourceFileName = sourceFileName.Replace('/', '\\');
                //string strBase = ConfigurationManager.AppSettings["ExportMapURL"].ToString();
                //sourceFileName = DateTime.Now.AddDays(-1).ToString("yyyyMMdd20") + "_" + DateTime.Now.AddDays(1).ToString("yyyyMMdd08") + "_diffusion_sh_mmdd.GIF";
                //sourceFileName = strBase + "\\" + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.ToString("yyyyMMddHH") + "\\" + sourceFileName;

                string strFTPIPString = null;
                string strFTPUser = null;
                string strFTPPSW = null;
                string strFtpIP = "";

                strFTPIPString = ftpURL;
                strFtpIP = strFTPIPString;
                strFTPUser = user;
                strFTPPSW = password;

                //上传的路径
                string upLoadFileName = sourceFileName.Split('\\')[sourceFileName.Split('\\').Length - 1];
                string strImgUrl = exportImageName;
                if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                {
                    if (strFtpIP.IndexOf('/') > 0)
                    {
                        strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                    }
                    //string uri = "ftp://" + strFTPIPString + "/";
                    Ftp ftp = new Ftp(strFTPIPString, strFTPUser, strFTPPSW);
                    ftp.Upload(strImgUrl, upLoadFileName);
                    return "success" + "&" + strImgUrl + "&" + upLoadFileName;
                }
            }
            return "fail";
        }

        //2015年12月6日,获取霾落区的数据
        public string GetHazeDropAreaData(string forecastDate)
        {
            string strImg = "";
            DateTime dtNow = DateTime.Now;
            string strForecastDate = dtNow.AddDays(-1).ToString("yyyy-MM-dd 20:00:00");
            string strSQL = "SELECT ('Product/'+Folder + '/' + Name) AS DM FROM dbo.T_LQHo WHERE ForecastDate='" + strForecastDate + "' and Period='" + "048" + "'" + "and Type='07'";
            DataSet ds = m_Database.GetDataset(strSQL);
            if (ds.Tables.Count > 0)
            {
                if (ds.Tables[0].Rows.Count > 0)
                {
                    for (int i = 0; i < ds.Tables[0].Rows.Count; i++)
                    {
                        strImg = strImg + ds.Tables[0].Rows[0][0].ToString();
                    }
                }
            }
            return strImg;
        }

        //刷新霾落区图片
        public string RefreshHazeDropAreaData(string imgUrl)
        {
            string strImgRefreshFtp = ConfigurationManager.AppSettings["ImgRefreshFtpPath"];
            string strURL = strImgRefreshFtp.Split(';')[0];
            string strUser = strImgRefreshFtp.Split(';')[1];
            string strPwd = strImgRefreshFtp.Split(';')[2];
            string strImgBasePath = ConfigurationManager.AppSettings["ImgProductBaseURL"];
            //string strImgBasePath =@"E:\浦东项目\浦东最新代码\SEMCShares\SEMCShares\WebUI\Product";
            StringBuilder sb = new StringBuilder();
            string sourceFileName = "";
            string strItemName = "";
            if (imgUrl != "" && imgUrl!=null)
            {
                sourceFileName = imgUrl;
                strItemName = "haze";
                string strTempFileName = sourceFileName.Substring(sourceFileName.LastIndexOf('/') + 1);
                string strSourceDate = strTempFileName.Split('_')[2];
                //起始日期增加的小时数
                string strIntervelHour = strTempFileName.Split('_')[3].Split('.')[0];
                if (sourceFileName.Contains("o3_1h") || sourceFileName.Contains("o3_8h"))
                {
                    strSourceDate = strTempFileName.Split('_')[3];
                    strIntervelHour = strTempFileName.Split('_')[4].Split('.')[0];
                }

                DateTime searchDate = new DateTime(Convert.ToInt32(strSourceDate.Substring(0, 4)), Convert.ToInt32(strSourceDate.Substring(4, 2)), Convert.ToInt32(strSourceDate.Substring(6, 2)), Convert.ToInt32(strSourceDate.Substring(8, 2)), 0, 0);
                int intIntervalHour = Convert.ToInt32(strIntervelHour);
                string strFtpSearchImgName = "";
                //if (strItemName == "pm25")
                //{
                //    strFtpSearchImgName = searchDate.ToString("yyyyMMddHH") + "_" + strItemName + "_" + searchDate.AddHours(intIntervalHour).ToString("MMdd") + ".GIF";
                //}
                //else
                //{
                //    strFtpSearchImgName = searchDate.ToString("yyyyMMddHH") + "_" + strItemName + "_" + searchDate.AddHours(intIntervalHour).ToString("MMdd") + ".GIF";
                //}
                strFtpSearchImgName = searchDate.ToString("yyyyMMddHH") + "_" + strItemName + "_" + searchDate.AddHours(intIntervalHour).ToString("MMdd") + ".GIF";
                string strImgSourceRealPath = sourceFileName.Replace("../Product", strImgBasePath);
                if (File.Exists(strImgSourceRealPath))
                {
                    File.Delete(strImgSourceRealPath);
                }
                Ftp ftp = new Ftp();
                ftp.DownloadToDifferentFileName(strUser, strPwd, strURL, strFtpSearchImgName, "", strImgSourceRealPath);                    
            }
            return sourceFileName;
        }

        //2015年12月7日
        public string QueryAQIDropZoneImgsCopy()
        {
            DateTime dtNow = DateTime.Now;
            //string strForecastDate = dtNow.AddDays(-1).ToString("yyyy-MM-dd 20:00:00");
            string strForecastDate = dtNow.AddDays(-1).ToString("yyyy-MM-dd 20:00:00");
            string strSQL = "SELECT Type,('Product/'+Folder + '/' + Name) AS DM FROM dbo.T_LQHo WHERE ForecastDate='" + strForecastDate + "' and Period='" + "048" + "'";
            DataSet ds = m_Database.GetDataset(strSQL);
            //标记污染物类型
            string strTypeId = "";
            StringBuilder sb = new StringBuilder();
            if (ds.Tables.Count > 0)
            {
                if (ds.Tables[0].Rows.Count > 0)
                {
                    for (int i = 0; i < ds.Tables[0].Rows.Count; i++)
                    {
                        if (ds.Tables[0].Rows[i][0].ToString() == "01")
                        {
                            strTypeId = "PM25Obj";
                        }
                        else if (ds.Tables[0].Rows[i][0].ToString() == "02")
                        {
                            strTypeId = "pm10";
                        }
                        else if (ds.Tables[0].Rows[i][0].ToString() == "03")
                        {
                            strTypeId = "no2";
                        }
                        else if (ds.Tables[0].Rows[i][0].ToString() == "04")
                        {
                            strTypeId = "o3_1h";
                        }
                        else if (ds.Tables[0].Rows[i][0].ToString() == "05")
                        {
                            strTypeId = "o3_8h";
                        }
                        else if (ds.Tables[0].Rows[i][0].ToString() == "06")
                        {
                            strTypeId = "pm25";
                        }
                        else if (ds.Tables[0].Rows[i][0].ToString() == "07")
                        {
                            strTypeId = "Haze";
                        }
                        else if (ds.Tables[0].Rows[i][0].ToString() == "08")
                        {
                            strTypeId = "Diffusion";
                        }
                        sb.Append("\"" + strTypeId + "\":\"" + ds.Tables[0].Rows[i][1].ToString() + "\",");
                    }
                }
            }
            return "{" + sb.ToString().Trim(',') + "}";

        }

        public string QueryAQIDropZoneImgs()
        {
            DateTime dtNow = DateTime.Now;
            //string strForecastDate = dtNow.AddDays(-1).ToString("yyyy-MM-dd 20:00:00");
            string strForecastDate = dtNow.AddDays(-1).ToString("yyyy-MM-dd 20:00:00");
            string strMaxDateSQL = "select MAX(Forecastdate) from T_LQHo";

            DataTable dtMaxTime = m_Database.GetDataTable(strMaxDateSQL);
            if (dtMaxTime.Rows.Count > 0)
            {
                strForecastDate = dtMaxTime.Rows[0][0].ToString();
            }
            else
            {
                strForecastDate = dtNow.AddDays(-1).ToString("yyyy-MM-dd 20:00:00");
            }
            
            string strSQL = "SELECT Type,('Product/'+Folder + '/' + Name) AS DM FROM dbo.T_LQHo WHERE ForecastDate='" + strForecastDate + "' and Period='" + "048" + "'";
            DataSet ds = m_Database.GetDataset(strSQL);
            //标记污染物类型
            string strTypeId = "";
            StringBuilder sb = new StringBuilder();
            if (ds.Tables.Count > 0)
            {
                if (ds.Tables[0].Rows.Count > 0)
                {
                    for (int i = 0; i < ds.Tables[0].Rows.Count; i++)
                    {
                        if (ds.Tables[0].Rows[i][0].ToString() == "01")
                        {
                            strTypeId = "PM25Obj";
                        }
                        else if (ds.Tables[0].Rows[i][0].ToString() == "02")
                        {
                            strTypeId = "pm10";
                        }
                        else if (ds.Tables[0].Rows[i][0].ToString() == "03")
                        {
                            strTypeId = "no2";
                        }
                        else if (ds.Tables[0].Rows[i][0].ToString() == "04")
                        {
                            strTypeId = "o3_1h";
                        }
                        else if (ds.Tables[0].Rows[i][0].ToString() == "05")
                        {
                            strTypeId = "o3_8h";
                        }
                        else if (ds.Tables[0].Rows[i][0].ToString() == "06")
                        {
                            strTypeId = "pm25";
                        }
                        else if (ds.Tables[0].Rows[i][0].ToString() == "07")
                        {
                            strTypeId = "Haze";
                        }
                        else if (ds.Tables[0].Rows[i][0].ToString() == "08")
                        {
                            strTypeId = "Diffusion";
                        }
                        sb.Append("\"" + strTypeId + "\":\"" + ds.Tables[0].Rows[i][1].ToString() + "\",");
                    }
                }
            }
            return "{" + sb.ToString().Trim(',') + "}";

        }

        //刷新获取AQI落区图片
        public string RefreshAQIDropZoneImgs(string imgURLs)
        {
            string strImgRefreshFtp = ConfigurationManager.AppSettings["ImgRefreshFtpPath"];
            string strURL = strImgRefreshFtp.Split(';')[0];
            string strUser = strImgRefreshFtp.Split(';')[1];
            string strPwd = strImgRefreshFtp.Split(';')[2];
            string strImgBasePath = ConfigurationManager.AppSettings["ImgProductBaseURL"];
            //string strImgBasePath =@"E:\浦东项目\浦东最新代码\SEMCShares\SEMCShares\WebUI\Product";
            StringBuilder sb = new StringBuilder();
            string [] imgs;
            if (imgURLs != "")
            {
                imgs = imgURLs.Split(',');
            }
            else
            {
                return "fail";
            }
            string sourceFileName = "";
            string strItemName = "";
            if(imgs.Length>0)
            {
                for(int m =0;m<imgs.Length;m++)
                {
                    sourceFileName = imgs[m].Split(':')[1];
                    strItemName = imgs[m].Split(':')[0];                    
                    string strTempFileName = sourceFileName.Substring(sourceFileName.LastIndexOf('/') + 1);
                    
                    string strSourceDate = strTempFileName.Split('_')[2];
                    //起始日期增加的小时数
                    string strIntervelHour = strTempFileName.Split('_')[3].Split('.')[0];
                    if (sourceFileName.Contains("o3_1h") || sourceFileName.Contains("o3_8h"))
                    {
                        strSourceDate = strTempFileName.Split('_')[3];
                        strIntervelHour = strTempFileName.Split('_')[4].Split('.')[0];
                    }

                    DateTime searchDate = new DateTime(Convert.ToInt32(strSourceDate.Substring(0, 4)), Convert.ToInt32(strSourceDate.Substring(4, 2)), Convert.ToInt32(strSourceDate.Substring(6, 2)), Convert.ToInt32(strSourceDate.Substring(8, 2)), 0, 0);
                    int intIntervalHour = Convert.ToInt32(strIntervelHour);
                    string strFtpSearchImgName="";
                    if (strItemName == "pm25")
                    {
                        strFtpSearchImgName = searchDate.ToString("yyyyMMddHH") + "_" + strItemName + "_" + searchDate.AddHours(intIntervalHour).ToString("MMdd") + ".GIF";
                    }
                    else
                    {
                        strFtpSearchImgName = searchDate.ToString("yyyyMMddHH") + "_" + strItemName + "_" + searchDate.AddHours(intIntervalHour).ToString("MMdd") + ".GIF";
                    }
                    string strImgSourceRealPath=sourceFileName.Replace("../Product",strImgBasePath);
                    if(File.Exists(strImgSourceRealPath))
                    {
                        File.Delete(strImgSourceRealPath);
                    }
                    Ftp ftp = new Ftp();
                    ftp.DownloadToDifferentFileName(strUser, strPwd, strURL, strFtpSearchImgName, "", strImgSourceRealPath);
                    sb.Append("\"" + strItemName + "\":\"" + sourceFileName + "\",");
                }
            }         
            return "{" + sb.ToString().Trim(',') + "}";

        }

        //2015年12月7日,获取空气污染气象条件的数据
        public string GetAirPollutionDropAreaData(string forecastDate)
        {
            string strImg = "";
            DateTime dtNow = DateTime.Now;
            string strForecastDate = dtNow.AddDays(-1).ToString("yyyy-MM-dd 20:00:00");
            string strSQL = "SELECT ('Product/'+Folder + '/' + Name) AS DM FROM dbo.T_LQHo WHERE ForecastDate='" + strForecastDate + "' and Period='" + "048" + "'" + "and Type='08'";
            DataSet ds = m_Database.GetDataset(strSQL);
            if (ds.Tables.Count > 0)
            {
                if (ds.Tables[0].Rows.Count > 0)
                {
                    for (int i = 0; i < ds.Tables[0].Rows.Count; i++)
                    {
                        strImg = strImg + ds.Tables[0].Rows[0][0].ToString();
                    }
                }
            }
            return strImg;
        }

        //刷新空气污染气象的图片
        public string RefreshAirPollutionDropAreaData(string imgUrl)
        {
            string strImgRefreshFtp = ConfigurationManager.AppSettings["ImgRefreshFtpPath"];
            string strURL = strImgRefreshFtp.Split(';')[0];
            string strUser = strImgRefreshFtp.Split(';')[1];
            string strPwd = strImgRefreshFtp.Split(';')[2];
            string strImgBasePath = ConfigurationManager.AppSettings["ImgProductBaseURL"];
            //string strImgBasePath =@"E:\浦东项目\浦东最新代码\SEMCShares\SEMCShares\WebUI\Product";
            StringBuilder sb = new StringBuilder();
            string sourceFileName = "";
            string strItemName = "";
            if (imgUrl != "" && imgUrl != null)
            {
                sourceFileName = imgUrl;
                strItemName = "diffusion";
                string strTempFileName = sourceFileName.Substring(sourceFileName.LastIndexOf('/') + 1);
                string strSourceDate = strTempFileName.Split('_')[2];
                //起始日期增加的小时数
                string strIntervelHour = strTempFileName.Split('_')[3].Split('.')[0];
                if (sourceFileName.Contains("o3_1h") || sourceFileName.Contains("o3_8h"))
                {
                    strSourceDate = strTempFileName.Split('_')[3];
                    strIntervelHour = strTempFileName.Split('_')[4].Split('.')[0];
                }

                DateTime searchDate = new DateTime(Convert.ToInt32(strSourceDate.Substring(0, 4)), Convert.ToInt32(strSourceDate.Substring(4, 2)), Convert.ToInt32(strSourceDate.Substring(6, 2)), Convert.ToInt32(strSourceDate.Substring(8, 2)), 0, 0);
                int intIntervalHour = Convert.ToInt32(strIntervelHour);
                string strFtpSearchImgName = "";
                //if (strItemName == "pm25")
                //{
                //    strFtpSearchImgName = searchDate.ToString("yyyyMMddHH") + "_" + strItemName + "_" + searchDate.AddHours(intIntervalHour).ToString("MMdd") + ".GIF";
                //}
                //else
                //{
                //    strFtpSearchImgName = searchDate.ToString("yyyyMMddHH") + "_" + strItemName + "_" + searchDate.AddHours(intIntervalHour).ToString("MMdd") + ".GIF";
                //}
                strFtpSearchImgName = searchDate.ToString("yyyyMMddHH") + "_" + strItemName + "_" + searchDate.AddHours(intIntervalHour).ToString("MMdd") + ".GIF";
                string strImgSourceRealPath = sourceFileName.Replace("../Product", strImgBasePath);
                if (File.Exists(strImgSourceRealPath))
                {
                    File.Delete(strImgSourceRealPath);
                }
                Ftp ftp = new Ftp();
                ftp.DownloadToDifferentFileName(strUser, strPwd, strURL, strFtpSearchImgName, "", strImgSourceRealPath);
            }
            return sourceFileName;
        }

        //2015年12月8日，获取污染天气过程未来三天霾的落区图片
        public string GetFutureThreeDayHazrDropImgs()
        {
            DateTime dtNow = DateTime.Now;
            string strForecastDate = dtNow.AddDays(-1).ToString("yyyy-MM-dd 20:00:00");
            string strSQL = "SELECT Period,('Product/'+Folder + '/' + Name) AS DM FROM dbo.T_LQHo WHERE ForecastDate='" + strForecastDate + "' and Type='07' order by Period";
            DataSet ds = m_Database.GetDataset(strSQL);
            StringBuilder sb = new StringBuilder();
            if (ds.Tables.Count > 0)
            {
                if (ds.Tables[0].Rows.Count > 0)
                {
                    for (int i = 0; i < ds.Tables[0].Rows.Count; i++)
                    {
                        sb.Append("\"" + ds.Tables[0].Rows[i][0].ToString() + "\":\"" + ds.Tables[0].Rows[i][1].ToString() + "\",");
                    }
                }
            }
            return "{" + sb.ToString().Trim(',') + "}";
        }

        #region 江西测试部分


        public string GetJiangXiReportContentPast()
        {
            string strContent = "011";
            strContent += "011\n";
            strContent += "58606 11592 2860 00469 16 0\n\n";
            strContent += "03 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "06 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "09 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "12 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "15 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "18 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "21 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "24 001493 001668 005071 000076 008713 002407 0050 1 399999\n";
            strContent += "30 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "36 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "42 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "48 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "54 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "60 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "66 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "72 999999 999999 999999 999999 999999 999999 9999 9 999999=\n";
            strContent += "57796 11493 2780 00820 16 09\n";
            strContent += "03 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "06 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "09 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "12 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "15 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "18 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "21 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "24 000678 001100 003581 000085 003816 001744 0036 1 099999\n";
            strContent += "30 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "36 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "42 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "48 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "54 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "60 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "66 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "72 999999 999999 999999 999999 999999 999999 9999 9 999999=\n";
            strContent += "58627 11703 2825 00558 16 09\n";
            strContent += "03 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "06 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "09 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "12 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "15 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "18 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "21 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "24 002184 000319 001813 000038 006135 001050 0031 1 099999\n";
            strContent += "30 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "36 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "42 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "48 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "54 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "60 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "66 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "72 999999 999999 999999 999999 999999 999999 9999 9 999999=\n";
            strContent += "57786 11385 2763 01175 16 09\n";
            strContent += "03 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "06 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "09 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "12 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "15 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "18 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "21 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "24 000750 000280 003335 000025 003778 001110 0033 1 099999\n";
            strContent += "30 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "36 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "42 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "48 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "54 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "60 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "66 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "72 999999 999999 999999 999999 999999 999999 9999 9 999999=\n";
            strContent += "58502 11600 2973 00361 16 09\n";
            strContent += "03 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "06 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "09 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "12 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "15 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "18 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "21 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "24 001137 002062 006387 000077 006228 003575 0057 2 399999\n";
            strContent += "30 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "36 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "42 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "48 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "54 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "60 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "66 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "72 999999 999999 999999 999999 999999 999999 9999 9 999999=\n";
            strContent += "58619 11637 2800 00493 16 09\n";
            strContent += "03 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "06 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "09 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "12 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "15 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "18 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "21 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "24 000629 001121 003379 000142 004711 002467 0035 1 099999\n";
            strContent += "30 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "36 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "42 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "48 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "54 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "60 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "66 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "72 999999 999999 999999 999999 999999 999999 9999 9 999999=\n";
            strContent += "58637 11798 2845 01182 16 09\n";
            strContent += "03 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "06 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "09 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "12 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "15 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "18 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "21 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "24 004972 000937 004038 000035 006878 003425 0050 1 099999\n";
            strContent += "30 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "36 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "42 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "48 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "54 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "60 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "66 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "72 999999 999999 999999 999999 999999 999999 9999 9 999999=\n";
            strContent += "57799 11492 2705 00712 16 09\n";
            strContent += "03 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "06 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "09 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "12 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "15 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "18 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "21 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "24 001018 000380 004144 000066 004424 002331 0041 1 099999\n";
            strContent += "30 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "36 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "42 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "48 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "54 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "60 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "66 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "72 999999 999999 999999 999999 999999 999999 9999 9 999999=\n";
            strContent += "57793 11438 2780 01313 16 09\n";
            strContent += "03 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "06 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "09 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "12 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "15 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "18 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "21 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "24 000681 000922 005582 000047 003090 004682 0065 2 699999\n";
            strContent += "30 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "36 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "42 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "48 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "54 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "60 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "66 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "72 999999 999999 999999 999999 999999 999999 9999 9 999999=\n";
            strContent += "57993 11500 2587 01375 16 09\n";
            strContent += "03 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "06 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "09 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "12 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "15 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "18 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "21 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "24 000702 001230 004338 000130 004252 002988 0043 1 099999\n";
            strContent += "30 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "36 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "42 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "48 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "54 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "60 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "66 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "72 999999 999999 999999 999999 999999 999999 9999 9 999999=\n";
            strContent += "58527 11720 2930 00615 16 09\n";
            strContent += "03 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "06 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "09 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "12 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "15 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "18 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "21 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "24 001468 000408 003739 000041 004682 001951 0037 1 099999\n";
            strContent += "30 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "36 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "42 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "48 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "54 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "60 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "66 999999 999999 999999 999999 999999 999999 9999 9 999999\n";
            strContent += "72 999999 999999 999999 999999 999999 999999 9999 9 999999=\n";
            strContent += "NNNN\n";
            return strContent;
        }

        //江西省模块内多个站点的预报文件
        public string GetJiangXiReportContent()
        {
            List<string> sitrList = new List<string>();
            sitrList.Add("58606");
            sitrList.Add("57796");
            sitrList.Add("58627");
            sitrList.Add("57786");
            sitrList.Add("58502");
            sitrList.Add("58619");
            sitrList.Add("58637");
            sitrList.Add("57799");
            sitrList.Add("57793");
            sitrList.Add("57993");
            sitrList.Add("58527");
            string strTotalContent = "";
            if (sitrList.Count < 10)
            {
                strTotalContent = "00" + sitrList.Count.ToString() + "\n";
            }
            else if (sitrList.Count >= 10 && sitrList.Count < 100)
            {
                strTotalContent = "0" + sitrList.Count.ToString() + "\n";
            }
            else
            {
                strTotalContent = sitrList.Count.ToString() + "\n";
            }
            string strSingleSiteText = "";
            string strMaxForeDate = "";
            string strMaxDateSQL = "select MAX(ForecastDate) from dbo.T_ForecastSite";
            DataTable dtMax = m_Database.GetDataTable(strMaxDateSQL);
            if (dtMax.Rows.Count > 0)
            {
                strMaxForeDate = dtMax.Rows[0][0].ToString();
            }
            for (int i = 0; i < sitrList.Count; i++)
            {

                strSingleSiteText = GetAQIAreaReportText(sitrList[i], strMaxForeDate);
                if (i < sitrList.Count - 1)
                {
                    strTotalContent += strSingleSiteText + "\n";
                }
                else
                {
                    strTotalContent += strSingleSiteText + "=" + "\r\nNNNN";
                }
            }
            return strTotalContent;
        }

        //上海分区AQI预报文件

        public string GetShanghaiReportContentCopy()
        {
            //DateTime dtNow = DateTime.Now.Date;
            //dtNow = dtNow.AddDays(1);
            ////if (forecastDate != "")
            ////    dtNow = DateTime.Parse(forecastDate);
            //string forecastDateTime = dtNow.ToString("yyyy-MM-dd 20:00:00");


            List<string> sitrList = new List<string>();
            sitrList.Add("58367");
            string strTotalContent = "";
            if (sitrList.Count < 10)
            {
                strTotalContent = "00" + sitrList.Count.ToString() + "\n";
            }
            else if (sitrList.Count >= 10 && sitrList.Count < 100)
            {
                strTotalContent = "0" + sitrList.Count.ToString() + "\n";
            }
            else
            {
                strTotalContent = sitrList.Count.ToString() + "\n";
            }
            string strSingleSiteText = "";
            string strMaxForeDate = "";
            string strMaxDateSQL = "select MAX(ForecastDate) from dbo.T_ForecastSite";

            DataTable dtMax = m_Database.GetDataTable(strMaxDateSQL);
            if (dtMax.Rows.Count > 0)
            {
                strMaxForeDate = dtMax.Rows[0][0].ToString();
            }
            for (int i = 0; i < sitrList.Count; i++)
            {

                strSingleSiteText = GetAQIAreaReportText(sitrList[i], strMaxForeDate);
                if (i < sitrList.Count - 1)
                {
                    strTotalContent += strSingleSiteText + "\n";
                }
                else
                {
                    strTotalContent += strSingleSiteText + "=" + "\r\nNNNN";
                }
            }
            return strTotalContent;
        }

        public string GetShanghaiReportContent()
        {
            //DateTime dtNow = DateTime.Now.Date;
            //dtNow = dtNow.AddDays(1);
            ////if (forecastDate != "")
            ////    dtNow = DateTime.Parse(forecastDate);
            //string forecastDateTime = dtNow.ToString("yyyy-MM-dd 20:00:00");


            List<string> sitrList = new List<string>();
            sitrList.Add("58367");
            string strTotalContent = "";
            if (sitrList.Count < 10)
            {
                strTotalContent = "00" + sitrList.Count.ToString() + "\n";
            }
            else if (sitrList.Count >= 10 && sitrList.Count < 100)
            {
                strTotalContent = "0" + sitrList.Count.ToString() + "\n";
            }
            else
            {
                strTotalContent = sitrList.Count.ToString() + "\n";
            }
            string strSingleSiteText = "";
            string strMaxForeDate = "";
            string strMaxDateSQL = "select MAX(ForecastDate) from dbo.T_ForecastSite";

            DataTable dtMax = m_Database.GetDataTable(strMaxDateSQL);
            if (dtMax.Rows.Count > 0)
            {
                strMaxForeDate = dtMax.Rows[0][0].ToString();
            }
            for (int i = 0; i < sitrList.Count; i++)
            {

                //strSingleSiteText = GetAQIAreaReportText(sitrList[i], strMaxForeDate);
                strSingleSiteText = GetAQIAreaReportTextNew(sitrList[i], strMaxForeDate);
                
                if (i < sitrList.Count - 1)
                {
                    strTotalContent += strSingleSiteText + "\n";
                }
                else
                {
                    strTotalContent += strSingleSiteText + "=" + "\r\nNNNN";
                }
            }
            return strTotalContent;
        }


        //获取江西空气质量指数
        public string GetJiangXiAirQuaIndexImg()
        {
            string strImg = "";
            string strSQL = "SELECT ('Product/'+Folder + '/' + Name) AS DM FROM T_JXLH WHERE Layers='01' order by ForecastDate desc";
            DataSet ds = m_Database.GetDataset(strSQL);

            if (ds.Tables.Count > 0)
            {
                if (ds.Tables[0].Rows.Count > 0)
                {
                    //for (int i = 0; i < ds.Tables[0].Rows.Count; i++)
                    //{
                    //    strImg = strImg + ds.Tables[0].Rows[0][0].ToString();
                    //}
                    strImg = strImg + ds.Tables[0].Rows[0][0].ToString();
                }
            }
            return strImg;
        }

        //获取江西空气污染气象条件(两张图)
        public string GetJiangXiAirPollutionTwo()
        {
            string strImg = "";
            //string strSQL = "SELECT Layers,('Product/'+Folder + '/' + Name) AS DM FROM T_JXLH WHERE Layers='02' or Layers='03'";

            string strSQL = "SELECT Layers,('Product/'+Folder + '/' + Name) AS DM FROM T_JXLH where Layers='02' AND ForecastDate=(select MAX(ForecastDate) from (select * from T_JXLH WHERE Layers='02') m)union all SELECT Layers,('Product/'+Folder + '/' + Name) AS DM FROM T_JXLH where Layers='03' AND ForecastDate=(select MAX(ForecastDate) from (select * from T_JXLH WHERE Layers='03') m)";
            DataSet ds = m_Database.GetDataset(strSQL);
            StringBuilder sb = new StringBuilder();
            if (ds.Tables.Count > 0)
            {
                if (ds.Tables[0].Rows.Count > 0)
                {
                    for (int i = 0; i < ds.Tables[0].Rows.Count; i++)
                    {
                        strImg = strImg + ds.Tables[0].Rows[0][0].ToString();
                        sb.Append("\"" + ds.Tables[0].Rows[i][0].ToString() + "\":\"" + ds.Tables[0].Rows[i][1].ToString() + "\",");
                    }
                }
            }
            return "{" + sb.ToString().Trim(',') + "}";
        }

        //获取江西空气污染气象条件(一张图)
        public string GetJiangXiAirPollutionSingle()
        {
            string strImg = "";
            //string strSQL = "SELECT ('Product/'+Folder + '/' + Name) AS DM FROM T_JXLH WHERE Layers='04'";
            string strSQL = "SELECT ('Product/'+Folder + '/' + Name) AS DM FROM T_JXLH WHERE Layers='04' order by ForecastDate desc";
            DataSet ds = m_Database.GetDataset(strSQL);

            if (ds.Tables.Count > 0)
            {
                if (ds.Tables[0].Rows.Count > 0)
                {
                    //for (int i = 0; i < ds.Tables[0].Rows.Count; i++)
                    //{
                    //    strImg = strImg + ds.Tables[0].Rows[0][0].ToString();
                    //}
                    strImg = strImg + ds.Tables[0].Rows[0][0].ToString();
                }
            }
            return strImg;
        }

        //2015年12月9日,将Word文档保存为PDF
        public string SaveWordAsPDF(string wordFilePath)
        {
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            wordFilePath = @"E:\浦东项目\环境分析平台制作发布产品.docx";
            if (wordFilePath != "")
            {
                string strFtpContent = ConfigurationManager.AppSettings["InfoCenterFtp"].ToString();
                if (strFtpContent != "")
                {
                    string[] ftpInfo = strFtpContent.Split(';');
                    strFTPIPString = ftpInfo[0].Split('=')[1];
                    strFtpIP = strFTPIPString;
                    strFTPUser = ftpInfo[1].Split('=')[1];

                    strFTPPSW = ftpInfo[2].Split('=')[1];
                }
                //string strFtpContent = ConfigurationManager.AppSettings["InfoCenterFtp"].ToString();                
                string strNewPath = @"E:\浦东项目\20151209\Test.pdf";
                WordHelper wordHelper = new WordHelper(wordFilePath);
                wordHelper.SaveAs(strNewPath, Aspose.Words.SaveFormat.Pdf);
                return "success";
            }
            return "fail";
        }


        //参数添加功能名称，用语在ftp上传表当中增添记录
        //public string UpLoadTxtFtpLatest(string ftpString, string functionName,string txtContent)
        //{
        //    //上传成功的数目
        //    int intSuccessCount = 0;
        //    //上传成功的数目
        //    int intFailCount = 0;
        //    if (txtContent == "" || txtContent == null)
        //    {
        //        return "fail";
        //    }
        //    string strFTPIPString = null;
        //    string strFTPUser = null;
        //    string strFTPPSW = null;
        //    string strFtpIP = "";

        //    if (ftpString != "")
        //    {
        //        string[] ftpList = { ftpString };

        //        if (ftpString.IndexOf(';') > 0)
        //        {
        //            ftpList = ftpString.Split(';');
        //        }
        //        //插入ftp上传日志表的SQL语句
        //        string strInsertSQL = "";
        //        if (ftpList.Length > 0)
        //        {
        //            for (int i = 0; i < ftpList.Length; i++)
        //            {

        //                string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
        //                if (strFtpContent != "")
        //                {
        //                    string[] ftpInfo = strFtpContent.Split(';');
        //                    strFTPIPString = ftpInfo[0].Split('=')[1];
        //                    strFtpIP = strFTPIPString;
        //                    strFTPUser = ftpInfo[1].Split('=')[1];
        //                    strFTPPSW = ftpInfo[2].Split('=')[1];
        //                    if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
        //                    {
        //                        if (strFtpIP.IndexOf('/') > 0)
        //                        {
        //                            strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
        //                        }
        //                        //存储的文件名
        //                        string strFileName = ftpList[i].Split(',')[1];
        //                        string strFileSuffix = strFileName.Split('.')[1];
        //                        if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
        //                        {
        //                            try
        //                            {
        //                                string strStart=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        //                                string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
        //                                string strPubState = strFtpResult == "success" ? "0" : "1";
        //                                //
        //                                string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"); 

        //                                //在状态表T_State当中插入记录
        //                                IPHostEntry ipe = Dns.GetHostEntry(Dns.GetHostName());
        //                                IPAddress ip = ipe.AddressList[0];

        //                                InsertFTPUpLoadLog("AQIProduct", "AQIPeriod", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, "user", ip.ToString(), strEnd, "Type");
        //                                //strInsertSQL = "INSERT INTO T_ProductLog (ProductType, ProductName, ReleaseType,StartTime,EndTime,State,Address,[User],IPAddress,Detail,DeadLine,Type) ";
        //                                //string strAQL = String.Format("SELECT '{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}'", "AQIPeriod", "AQIPeriod", "FTP", strStart, strEnd, strPubState, strFTPIPString, "User", "IPAddress", "Detail", strEnd, "Type");
        //                                //strInsertSQL += strAQL;
        //                                //m_Database.Execute(strInsertSQL);
        //                                intSuccessCount++;
        //                            }
        //                            catch (Exception e)
        //                            {
        //                                intFailCount++;
        //                            }

        //                        }
        //                    }
        //                }
        //            }
        //            intFailCount = ftpList.Length - intSuccessCount;
        //        }
        //    }
        //    return "成功" + intSuccessCount.ToString() + "个，失败" + intFailCount.ToString() + "个";
        //}

        //只上传文件到ftp的全局方法，
        public string UpLoadFileFtp(string ftpString, string txtContent)
        {
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            if (txtContent == "" || txtContent == null)
            {
                return "fail";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                string strFileSuffix = strFileName.Split('.')[1];

                                if (strFileSuffix == "doc" || strFileSuffix == "docx" || strFileSuffix == "PDF" || strFileSuffix == "GIF")
                                {
                                    try
                                    {
                                        UpLoadFileToFTP(strFTPIPString, "sourFileName", strFileName, strFTPUser, strFTPPSW);
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;
                }
            }
            return "成功" + intSuccessCount.ToString() + "个，失败" + intFailCount.ToString() + "个";
        }

        //页面内既要上传txt又要上传文件的方法
        public string UpLoadAll(string ftpString, string txtContent)
        {
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            if (txtContent == "" || txtContent == null)
            {
                return "fail";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                }
                                else if (strFileSuffix == "doc" || strFileSuffix == "docx" || strFileSuffix == "PDF" || strFileSuffix == "GIF")
                                {
                                    UpLoadFileToFTP(strFTPIPString, "sourceFileName", strFileName, strFTPUser, strFTPPSW);
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;
                }
            }
            return "成功" + intSuccessCount.ToString() + "个，失败" + intFailCount.ToString() + "个";
        }
        #endregion

        //2015年12月10日，上传文件(Word,GIF,PDF)到ftp
        private void UpLoadFileToFTP(string ftpURL, string sourceFileName, string newFileName, string ftpUser, string ftpPassword)
        {
            FileInfo fileInf = new FileInfo(sourceFileName);
            FtpWebRequest reqFTP;
            // 根据uri创建FtpWebRequest对象 
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpURL + newFileName));
            // ftp用户名和密码
            reqFTP.Credentials = new NetworkCredential(ftpUser, ftpPassword);

            reqFTP.UsePassive = false;
            // 默认为true，连接不会被关闭
            // 在一个命令之后被执行
            reqFTP.KeepAlive = false;
            // 指定执行什么命令
            reqFTP.Method = WebRequestMethods.Ftp.UploadFile;
            // 指定数据传输类型
            reqFTP.UseBinary = true;
            // 上传文件时通知服务器文件的大小
            reqFTP.ContentLength = fileInf.Length;
            // 缓冲大小设置为2kb
            int buffLength = 2048;
            byte[] buff = new byte[buffLength];
            int contentLen;
            // 打开一个文件流 (System.IO.FileStream) 去读上传的文件
            FileStream fs = fileInf.OpenRead();
            try
            {
                // 把上传的文件写入流
                Stream strm = reqFTP.GetRequestStream();
                // 每次读文件流的2kb
                contentLen = fs.Read(buff, 0, buffLength);
                // 流内容没有结束
                while (contentLen != 0)
                {
                    // 把内容从file stream 写入 upload stream
                    strm.Write(buff, 0, contentLen);
                    contentLen = fs.Read(buff, 0, buffLength);
                }
                // 关闭两个流
                strm.Close();
                fs.Close();
            }
            catch (Exception ex)
            {

            }
        }

        //将Txt上传到FTP
        private string UpLoadTxtToFTP(string ftpURL, string fileName, string user, string password, string txtContent)
        {
            string strNew = System.Text.RegularExpressions.Regex.Replace(txtContent, "<[^>]*>", "");
            strNew = strNew.Replace("$nbsp", " ");
            strNew = AddHuanhang(txtContent);
            byte[] array = System.Text.Encoding.Default.GetBytes(strNew);
            //byte[] array = System.Text.Encoding.ASCII.GetBytes(strNew);
            MemoryStream stream = new MemoryStream(array);
            StreamReader reader = new StreamReader(stream);
            string[] vars = txtContent.Split('\n');
            txtContent.Replace('\n', '\r');
            string text = reader.ReadToEnd();

            string uri = "ftp://" + ftpURL + "/" + fileName;
            FtpWebRequest reqFTP;
            // Create FtpWebRequest object from the Uri provided 
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(uri));
            try
            {
                reqFTP.Credentials = new NetworkCredential(user, password);
                reqFTP.KeepAlive = false;
                reqFTP.Method = WebRequestMethods.Ftp.UploadFile;
                reqFTP.UseBinary = true;
                reqFTP.ContentLength = array.Length;
                Stream strm = reqFTP.GetRequestStream();
                strm.Write(array, 0, array.Length);
                strm.Close();
                return "success";
            }
            catch (Exception ex)
            {
                reqFTP.Abort();
                return "fail";
            }
        }

        private string UpLoadTxtToFTPAQIPeriodCopy(string ftpURL, string fileName, string user, string password, string txtContent)
        {
            string strNew = System.Text.RegularExpressions.Regex.Replace(txtContent, "<[^>]*>", "");
            strNew = strNew.Replace("$nbsp", " ");
            strNew = AddHuanhang(txtContent);

            //byte[] array = System.Text.Encoding.GetEncoding("GB2312").GetBytes(strNew);
            byte[] array = System.Text.Encoding.Default.GetBytes(strNew);
            //byte[] array = System.Text.Encoding.ASCII.GetBytes(strNew);
            MemoryStream stream = new MemoryStream(array);
            StreamReader reader = new StreamReader(stream);
            string[] vars = txtContent.Split('\n');
            txtContent.Replace('\n', '\r');
            string text = reader.ReadToEnd();

            string uri = "ftp://" + ftpURL + "/" + fileName;
            FtpWebRequest reqFTP;
            // Create FtpWebRequest object from the Uri provided 
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(uri));
            try
            {
                reqFTP.Credentials = new NetworkCredential(user, password);
                reqFTP.KeepAlive = false;
                reqFTP.Method = WebRequestMethods.Ftp.UploadFile;
                reqFTP.UseBinary = true;
                reqFTP.ContentLength = array.Length;
                Stream strm = reqFTP.GetRequestStream();
                strm.Write(array, 0, array.Length);
                strm.Close();
                return "success";
            }
            catch (Exception ex)
            {
                reqFTP.Abort();
                return "fail";
            }
        }

        private string UpLoadTxtToFTPAQIPeriod(string ftpURL, string fileName, string user, string password, string txtContent)
        {
            //string strTempFilePath = ConfigurationManager.AppSettings["AQIPeriodTempPath"].ToString();
            //string strFilePrefix = "AQIPeriod";
            //string strType = "Text_";

            //string strFilePath = strTempFilePath + strFilePrefix + strType + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
            //if (File.Exists(strFilePath))
            //{
            //    try
            //    {
            //        Ftp ftp = new Ftp(ftpURL, user, password);
            //        ftp.Upload(strFilePath, fileName);
            //        return "success";
            //    }
            //    catch {
            //        return "fail";
            //    }
            //}
            //return "fail";

            string strNew = System.Text.RegularExpressions.Regex.Replace(txtContent, "<[^>]*>", "");
            strNew = strNew.Replace("$nbsp", " ");
            strNew = AddHuanhang(txtContent);

            //byte[] array = System.Text.Encoding.GetEncoding("GB2312").GetBytes(strNew);
            byte[] array = System.Text.Encoding.Default.GetBytes(strNew);
            //byte[] array = System.Text.Encoding.ASCII.GetBytes(strNew);
            MemoryStream stream = new MemoryStream(array);
            StreamReader reader = new StreamReader(stream);
            string[] vars = txtContent.Split('\n');
            txtContent.Replace('\n', '\r');
            string text = reader.ReadToEnd();

            string uri = "ftp://" + ftpURL + "/" + fileName;
            FtpWebRequest reqFTP;
            // Create FtpWebRequest object from the Uri provided 
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(uri));
            try
            {
                reqFTP.Credentials = new NetworkCredential(user, password);
                reqFTP.KeepAlive = false;
                reqFTP.Method = WebRequestMethods.Ftp.UploadFile;
                reqFTP.UseBinary = true;
                reqFTP.ContentLength = array.Length;
                Stream strm = reqFTP.GetRequestStream();
                strm.Write(array, 0, array.Length);
                strm.Close();
                return "success";
            }
            catch (Exception ex)
            {
                reqFTP.Abort();
                return "fail";
            }
        }

        //按照新版文档上传短信的方法
        private string UpLoadTxtToFTPNewVersionMsg(string ftpURL, string fileName, string user, string password, string txtContent)
        {
            //string strNew = System.Text.RegularExpressions.Regex.Replace(txtContent, "<[^>]*>", "");
            //strNew = strNew.Replace("$nbsp", " ");
            //strNew = AddHuanhang(txtContent);
            //byte[] array = System.Text.Encoding.Default.GetBytes(strNew);
            Encoding gb2312 = Encoding.GetEncoding(936);
            //byte[] array = gb2312.GetBytes(strNew);
            byte[] array = gb2312.GetBytes(txtContent);
            //byte[] array = System.Text.Encoding.ASCII.GetBytes(strNew);
            MemoryStream stream = new MemoryStream(array);
            StreamReader reader = new StreamReader(stream);
            string[] vars = txtContent.Split('\n');
            txtContent.Replace('\n', '\r');
            string text = reader.ReadToEnd();

            string uri = "ftp://" + ftpURL + "/" + fileName;
            FtpWebRequest reqFTP;
            // Create FtpWebRequest object from the Uri provided 
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(uri));
            try
            {
                reqFTP.Credentials = new NetworkCredential(user, password);
                reqFTP.KeepAlive = false;
                reqFTP.Method = WebRequestMethods.Ftp.UploadFile;
                reqFTP.UseBinary = true;
                reqFTP.ContentLength = array.Length;
                Stream strm = reqFTP.GetRequestStream();
                strm.Write(array, 0, array.Length);
                strm.Close();
                return "success";
            }
            catch (Exception ex)
            {
                reqFTP.Abort();
                return "fail";
            }
        }


        //2015年12月15日，每上传一次ftp，在T_ProductLog表内插入一条记录
        /// <summary>
        /// 
        /// </summary>
        /// <param name="productType">产品大类名称</param>
        /// <param name="productName">产品具体名称</param>
        /// <param name="startTime">上传开始时间</param>
        /// <param name="endTime">上传结束时间</param>
        /// <param name="state">发布状态，含义在文档内</param>
        /// <param name="ftpIP">ftp路径</param>
        /// <param name="user">ftp用户名</param>
        /// <param name="localIP">上传人所在IP</param>
        /// <param name="deadLine">截止时间</param>
        /// <param name="type">产品类型（能否更改，默认为2）</param>
        public void InsertFTPUpLoadLog(string productType, string productName, string startTime, string endTime, string state, string ftpIP, string user, string localIP, string deadLine, string type)
        {
            //在状态表T_State当中插入记录
            try
            {
                //string strProNameCN = "";
                //switch (productName)
                //{
                //    case "AQIPeriod":
                //        strProNameCN="AQI分时段预报文本";
                //        break;
                //    case "AQIArea":
                //        strProNameCN = "AQI分区预报";
                //        break;
                //    case "HazeForecast":
                //        strProNameCN = "霾预报";
                //        break;
                //    case "UVForecast":
                //        strProNameCN = "紫外线预报";
                //        break;
                //    case "OzoneForecast":
                //        strProNameCN = "臭氧预报";
                //        break;
                //    case "AirPollutionForecast":
                //        strProNameCN = "空气污染气象条件预报";
                //        break;
                //    case "HazeWarning":
                //        strProNameCN = "霾预警";
                //        break;
                //    case "OzoneWarning":
                //        strProNameCN = "臭氧预警";
                //        break;
                //    case "PolWeatherAnalysisReport":
                //        strProNameCN = "污染天气过程跟踪解析";
                //        break;
                //    case "AQIDropZone":
                //        strProNameCN = "AQI落区";
                //        break;
                //    case "AQIGuideReport":
                //        strProNameCN = "站点指导预报";
                //        break;
                //    case "HazeDropZone":
                //        strProNameCN = "霾落区";
                //        break;
                //    case "AirPollutionDropZone":
                //        strProNameCN = "空气污染落区";
                //        break;
                //    case "ImportantWeatherReport":
                //        strProNameCN = "重要天气专报";
                //        break;
                //    case "WeekPolWeather":
                //        strProNameCN = "一周污染天气展望";
                //        break;
                //    case "MainCityForecast":
                //        strProNameCN = "重点城市专报";
                //        break;
                //}
                string strInsertSQL = "INSERT INTO T_ProductLog (ProductType, ProductName, ReleaseType,StartTime,EndTime,State,Address,[User],IPAddress,Detail,DeadLine,Type) ";
                string strAQL = String.Format("SELECT '{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}'", productType, productName, "FTP", startTime, endTime, state, ftpIP, user, localIP, "Detail", deadLine, type);
                strInsertSQL += strAQL;
                m_Database.Execute(strInsertSQL);
            }
            catch (Exception e)
            {

            }
        }

        public void InsertFTPUpLoadLogNew(string productType, string productName, string startTime, string endTime, string state, string ftpIP, string user, string localIP, string deadLine, string type, string fileTempPath)
        {
            //在状态表T_State当中插入记录
            try
            {
                //string strProNameCN = "";
                //switch (productName)
                //{
                //    case "AQIPeriod":
                //        strProNameCN="AQI分时段预报文本";
                //        break;
                //    case "AQIArea":
                //        strProNameCN = "AQI分区预报";
                //        break;
                //    case "HazeForecast":
                //        strProNameCN = "霾预报";
                //        break;
                //    case "UVForecast":
                //        strProNameCN = "紫外线预报";
                //        break;
                //    case "OzoneForecast":
                //        strProNameCN = "臭氧预报";
                //        break;
                //    case "AirPollutionForecast":
                //        strProNameCN = "空气污染气象条件预报";
                //        break;
                //    case "HazeWarning":
                //        strProNameCN = "霾预警";
                //        break;
                //    case "OzoneWarning":
                //        strProNameCN = "臭氧预警";
                //        break;
                //    case "PolWeatherAnalysisReport":
                //        strProNameCN = "污染天气过程跟踪解析";
                //        break;
                //    case "AQIDropZone":
                //        strProNameCN = "AQI落区";
                //        break;
                //    case "AQIGuideReport":
                //        strProNameCN = "站点指导预报";
                //        break;
                //    case "HazeDropZone":
                //        strProNameCN = "霾落区";
                //        break;
                //    case "AirPollutionDropZone":
                //        strProNameCN = "空气污染落区";
                //        break;
                //    case "ImportantWeatherReport":
                //        strProNameCN = "重要天气专报";
                //        break;
                //    case "WeekPolWeather":
                //        strProNameCN = "一周污染天气展望";
                //        break;
                //    case "MainCityForecast":
                //        strProNameCN = "重点城市专报";
                //        break;
                //}
                string strInsertSQL = "INSERT INTO T_ProductLog (ProductType, ProductName, ReleaseType,StartTime,EndTime,State,Address,[User],IPAddress,Detail,DeadLine,Type,FileTempPath) ";
                string strAQL = String.Format("SELECT '{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}','{12}'", productType, productName, "FTP", startTime, endTime, state, ftpIP, user, localIP, "Detail", deadLine, type, fileTempPath);
                strInsertSQL += strAQL;
                m_Database.Execute(strInsertSQL);
            }
            catch (Exception e)
            {

            }
        }

        //2015年12月16日
        public string LoadHistoryHaze()
        {
            StringBuilder sb = new StringBuilder();
            string strSQL = "select LST,Haze,Vis FROM T_Haze WHERE ReTime=(select MAX(ReTime) from T_Haze) order by LST";
            DataTable dt = m_Database.GetDataTable(strSQL);
            if (dt.Rows.Count > 0)
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    string strLST = DateTime.Parse(dt.Rows[i]["LST"].ToString()).ToString("yyyy年MM月dd日");
                    if (strLST != "")
                    {
                        string month = strLST.Split('年')[1].Split('月')[0];
                        //月份以0开头
                        if (month.IndexOf('0') == 0)
                        {
                            strLST = strLST.Replace(month + "月", Convert.ToInt32(month).ToString() + "月");
                        }
                    }
                    sb.Append("{\"LST\":\"" + strLST + "\",\"Haze\":\"" + dt.Rows[i]["Haze"].ToString() + "\",\"Vis\":\"" + dt.Rows[i]["Vis"].ToString() + "\"},");
                }
            }
            return "[" + sb.ToString().Trim(',') + "]";
        }

        //2015年12月20日，获取紫外线历史数据
        public string LoadHistoryUV()
        {
            StringBuilder sb = new StringBuilder();
            string strSQL = "select LST,UVAB,[Index] FROM T_TbUVS WHERE LST=(select MAX(LST) from T_TbUVS)";
            DataTable dt = m_Database.GetDataTable(strSQL);
            if (dt.Rows.Count > 0)
            {
                string strUVAB = dt.Rows[dt.Rows.Count - 1]["UVAB"].ToString();
                string strUVIndex = dt.Rows[dt.Rows.Count - 1]["Index"].ToString();
                return strUVAB + "_" + strUVIndex;
            }
            return "";
        }

        //第二天上传的紫外线预报文件
        //public string UploadTomorrowUV(string ftpString, string fileDate, string functionName,string userName)
        //{
        //    //DateTime date = GetDatetime(fileDate);
        //    //string strDate = date.AddDays(-1).ToString("yyyyMMdd");
        //    string strDate = "";
        //    DateTime pubDate = DateTime.Now;
        //    //文本模板
        //    string strTemplate = "58362 1{UVLevel}000 20{10}";
        //    if (fileDate != "")
        //    {
        //        strDate = GetDatetime(fileDate).AddDays(-1).ToString("yyyy-MM-dd HH:00:00.000");
        //        pubDate = GetDatetime(fileDate);
        //    }
        //    else
        //    {
        //        strDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd HH:00:00.000");
        //        pubDate = DateTime.Now;
        //    }
        //    StringBuilder sb = new StringBuilder();
        //    string strSQL = "select LST,UVAB,[Index] FROM T_TbUVS WHERE LST='" + strDate + "'";
        //    DataTable dt = m_Database.GetDataTable(strSQL);
        //    string strUVLevel = "";
        //    string strUVPadValue = "";
        //    if (dt.Rows.Count > 0)
        //    {
        //        string strUVIndex = dt.Rows[dt.Rows.Count - 1]["Index"].ToString();
        //        string strUVAB = dt.Rows[dt.Rows.Count - 1]["UVAB"].ToString();
        //        int intLevel = Convert.ToInt32(strUVIndex);
        //        double dblUVValue = Convert.ToDouble(strUVAB);
        //        strUVPadValue = dblUVValue.ToString();
        //        if (intLevel >= 0 && intLevel <= 2)
        //        {
        //            strUVLevel = "1";
        //        }
        //        else if (intLevel >= 3 && intLevel <= 4)
        //        {
        //            strUVLevel = "2";
        //        }
        //        else if (intLevel >= 5 && intLevel <= 6)
        //        {
        //            strUVLevel = "3";
        //        }
        //        else if (intLevel >= 7 && intLevel <= 9)
        //        {
        //            strUVLevel = "4";
        //        }
        //        else if (intLevel >= 10)
        //        {
        //            strUVLevel = "5";
        //        }

        //        if (dblUVValue == 0)
        //        {
        //            strUVPadValue= "000";
        //        }
        //        strUVPadValue = strUVPadValue + "";
        //        if (strUVPadValue.IndexOf('.') == -1)
        //        {
        //            strUVPadValue = strUVPadValue + ".0";
        //        }
        //        strUVPadValue = strUVPadValue.Replace(".", "");
        //        strUVPadValue = strUVPadValue.Replace(".", "");
        //        int len = strUVPadValue.Length;
        //        while (len < 3)
        //        {
        //            strUVPadValue = "0" + strUVPadValue; 
        //            len++; 
        //        }                
        //        strTemplate = strTemplate.Replace("{UVLevel}", strUVLevel);
        //        strTemplate = strTemplate.Replace("{10}", strUVPadValue);

        //    }

        //    if (strTemplate != "")
        //    {
        //        //上传成功的数目
        //        int intSuccessCount = 0;
        //        //上传成功的数目
        //        int intFailCount = 0;
        //        string strPubDate = "";

        //    string strFTPIPString = null;
        //    string strFTPUser = null;
        //    string strFTPPSW = null;
        //    string strFtpIP = "";

        //    if (ftpString != "")
        //    {
        //        string[] ftpList = { ftpString };

        //        if (ftpString.IndexOf(';') > 0)
        //        {
        //            ftpList = ftpString.Split(';');
        //        }

        //        if (ftpList.Length > 0)
        //        {
        //            for (int i = 0; i < ftpList.Length; i++)
        //            {

        //                string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
        //                if (strFtpContent != "")
        //                {
        //                    string[] ftpInfo = strFtpContent.Split(';');
        //                    strFTPIPString = ftpInfo[0].Split('=')[1];
        //                    strFtpIP = strFTPIPString;
        //                    strFTPUser = ftpInfo[1].Split('=')[1];
        //                    strFTPPSW = ftpInfo[2].Split('=')[1];
        //                    if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
        //                    {
        //                        if (strFtpIP.IndexOf('/') > 0)
        //                        {
        //                            strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
        //                        }
        //                        //存储的文件名
        //                        string strFileName = ftpList[i].Split(',')[1];
        //                        strFileName = strFileName.Replace("07", "01");
        //                        if (strFileName.Contains("YYYYMMddhhmmss"))
        //                        {
        //                            strPubDate = pubDate.ToString("yyyyMMddhhmmss");
        //                            strFileName = strFileName.Replace("YYYYMMddhhmmss", strPubDate);
        //                        }
        //                        if (strFileName.Contains("YYYYMMDDHHmm"))
        //                        {
        //                            strPubDate = pubDate.ToString("yyyyMMddHHmm");
        //                            strFileName = strFileName.Replace("YYYYMMDDHHmm", strPubDate);
        //                        }
        //                        if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
        //                        {
        //                            strPubDate = pubDate.ToString("yyyyMMddHH");
        //                            strFileName = strFileName.Replace("YYYYMMDDHH", strPubDate);
        //                            strFileName = strFileName.Replace("YyYyMmDdHh", strPubDate);
        //                        }
        //                        if (strFileName.Contains("YYYYMMDD"))
        //                        {
        //                            strPubDate = pubDate.ToString("yyyyMMdd");
        //                            strFileName = strFileName.Replace("YYYYMMDD", strPubDate);
        //                        }
        //                        if (strFileName.Contains("YYMMDD"))
        //                        {
        //                            strPubDate = pubDate.ToString("yyMMdd");
        //                            strFileName = strFileName.Replace("YYMMDD", strPubDate);
        //                        }
        //                        if (strFileName.Contains("mmdd"))
        //                        {
        //                            strPubDate = pubDate.ToString("MMdd");
        //                            strFileName = strFileName.Replace("mmdd", strPubDate);
        //                        }
        //                        if (strFileName.Contains("MMDD"))
        //                        {
        //                            strPubDate = pubDate.ToString("MMdd");
        //                            strFileName = strFileName.Replace("MMDD", strPubDate);
        //                        }

        //                        //文件后缀名
        //                        string strFileSuffix = strFileName.Split('.')[1];
        //                        if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
        //                        {
        //                            try
        //                            {
        //                                string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        //                                string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, strTemplate);
        //                                string strPubState = strFtpResult == "success" ? "0" : "1";
        //                                //
        //                                string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        //                                //在状态表T_State当中插入记录
        //                                string strIP = HttpClientHelper.GetIP();
        //                                InsertFTPUpLoadLog("AQIProduct", functionName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
        //                                intSuccessCount++;
        //                            }
        //                            catch (Exception e)
        //                            {
        //                                intFailCount++;
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //            intFailCount = ftpList.Length - intSuccessCount;
        //            string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
        //            string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
        //            if (functionName == "UVForecast")
        //            {
        //                strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 09:45:00.000");
        //                strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 12:45:00.000");
        //            }
        //            else if (functionName == "OzoneForecast")
        //            {
        //                strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:40:00.000");
        //                strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 19:40:00.000");
        //            }
        //            if (intSuccessCount == ftpList.Length)
        //            {
        //                InsertIntoStateTable(functionName+"_05", strRcdTime, strDeadLineTime, "3", "2");
        //                //表示全部发布成功
        //                return "success";
        //            }
        //            else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
        //            {
        //                InsertIntoStateTable(functionName + "_05", strRcdTime, strDeadLineTime, "4", "2");
        //                //表示发布不完全
        //                return "less";
        //            }


        //            //if (intSuccessCount == ftpList.Length)
        //            //{
        //            //    //表示全部发布成功
        //            //    return "success";
        //            //}
        //            //else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
        //            //{
        //            //    //表示发布不完全
        //            //    return "less";
        //            //}
        //        }
        //    }
        //    }
        //    return "fail";
        //}

        //2015年12月20日，获取臭氧历史数据
        public string LoadHistoryOzone()
        {
            StringBuilder sb = new StringBuilder();
            string strSQL = "select LST,O3,O38,O3Period,O38Period FROM T_Ozone WHERE LST=(select MAX(LST) from T_Ozone)";
            DataTable dt = m_Database.GetDataTable(strSQL);
            if (dt.Rows.Count > 0)
            {

                string strLST = DateTime.Parse(dt.Rows[dt.Rows.Count - 1]["LST"].ToString()).ToString("yyyy年MM月dd日");
                sb.Append("{\"LST\":\"" + strLST + "\",\"O3\":\"" + dt.Rows[dt.Rows.Count - 1]["O3"].ToString() + "\",\"O38\":\"" + dt.Rows[dt.Rows.Count - 1]["O38"].ToString() + "\",\"O3Period\":\"" + dt.Rows[dt.Rows.Count - 1]["O3Period"].ToString() + "\",\"O38Period\":\"" + dt.Rows[dt.Rows.Count - 1]["O38Period"].ToString() + "\"},");

            }
            return sb.ToString().Trim(',');

        }

        //保存空气污染气象预报        

        public string SaveAirPollutionForecastCopy(string data, string strForecastDate, string strPeriod)
        {

            string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
            string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");

            DateTime dtNow = DateTime.Now;
            strForecastDate = dtNow.ToString("yyyy-MM-dd 00:00:00");

            if (IsPublished("AirPollutionForecast", strRcdTime) == true)
            {
                return "published";
            }

            //区域名称
            string strAreaName = "null";
            string strPM25 = "null";
            string strAQIQuaLevel = "null";
            string strAirPolConLevel = "null";
            StringBuilder sb = new StringBuilder("INSERT INTO T_AirMeto (Area, [PM2.5], AirQ,AirPo,ForecastDate,PERIOD)");
            if (data != "")
            {
                //data = data.TrimEnd(',');
                string[] cells = data.Split('&');

                for (int i = 0; i < cells.Length; i++)
                {

                    string[] strSingleArea = cells[i].Split('_');

                    switch (strSingleArea[0])
                    {
                        case "58367":
                            strAreaName = "中心城区";
                            break;
                        case "58370":
                            strAreaName = "浦东新区";
                            break;
                        case "58361":
                            strAreaName = "闵行区";
                            break;
                        case "58362":
                            strAreaName = "宝山区";
                            break;
                        case "58462":
                            strAreaName = "松江区";
                            break;
                        case "58460":
                            strAreaName = "金山区";
                            break;
                        case "58461":
                            strAreaName = "青浦区";
                            break;
                        case "58463":
                            strAreaName = "奉贤区";
                            break;
                        case "58365":
                            strAreaName = "嘉定区";
                            break;
                        case "58366":
                            strAreaName = "崇明县";
                            break;
                        default:
                            strAreaName = "中心城区";
                            break;
                    }
                    strPM25 = strSingleArea[1];

                    switch (strSingleArea[2])
                    {
                        case "一级":
                            strAQIQuaLevel = "1";
                            break;
                        case "二级":
                            strAQIQuaLevel = "2";
                            break;
                        case "三级":
                            strAQIQuaLevel = "3";
                            break;
                        case "四级":
                            strAQIQuaLevel = "4";
                            break;
                        case "五级":
                            strAQIQuaLevel = "5";
                            break;
                        case "六级":
                            strAQIQuaLevel = "6";
                            break;
                        default:
                            strAQIQuaLevel = "1";
                            break;
                    }

                    switch (strSingleArea[3])
                    {
                        case "一级":
                            strAirPolConLevel = "1";
                            break;
                        case "二级":
                            strAirPolConLevel = "2";
                            break;
                        case "三级":
                            strAirPolConLevel = "3";
                            break;
                        case "四级":
                            strAirPolConLevel = "4";
                            break;
                        case "五级":
                            strAirPolConLevel = "5";
                            break;
                        case "六级":
                            strAirPolConLevel = "6";
                            break;
                        default:
                            strAirPolConLevel = "1";
                            break;
                    }
                    if (i == 0)
                    {
                        sb.Append(string.Format(" SELECT '{0}','{1}','{2}','{3}','{4}','{5}'", strAreaName, strPM25, strAQIQuaLevel, strAirPolConLevel, strForecastDate, strPeriod));
                    }
                    else
                    {
                        sb.Append(string.Format(" UNION ALL SELECT '{0}','{1}','{2}','{3}','{4}','{5}'", strAreaName, strPM25, strAQIQuaLevel, strAirPolConLevel, strForecastDate, strPeriod));

                    }
                }
                m_Database.Execute("delete from T_AirMeto where ForecastDate='" + strForecastDate + "'");
                m_Database.Execute(sb.ToString());


                InsertIntoStateTable("AirPollutionForecast", strRcdTime, strDeadLineTime, "1", "2");
                return "success";
            }
            return "fail";
        }

        public string SaveAirPollutionForecast(string data, string strForecastDate, string strPeriod, string hourType)
        {
            string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
            string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
            string functionName = "AirPollutionForecast_05";
            if (hourType == "05")
            {
                strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 05:00:00.000");
                strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 08:00:00.000");
                functionName = "AirPollutionForecast_05";
            }
            else if (hourType == "17")
            {
                strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                functionName = "AirPollutionForecast_17";
            }

            DateTime dtNow = DateTime.Now;
            strForecastDate = dtNow.ToString("yyyy-MM-dd 00:00:00");

            if (IsPublished(functionName, strRcdTime) == true)
            {
                return "published";
            }

            //区域名称
            string strAreaName = "null";
            string strPM25 = "null";
            string strAQIQuaLevel = "null";
            string strAirPolConLevel = "null";
            StringBuilder sb = new StringBuilder("INSERT INTO T_AirMeto (Area, [PM2.5], AirQ,AirPo,ForecastDate,PERIOD)");
            if (data != "")
            {
                //data = data.TrimEnd(',');
                string[] cells = data.Split('&');

                for (int i = 0; i < cells.Length; i++)
                {
                    string[] strSingleArea = cells[i].Split('_');

                    switch (strSingleArea[0])
                    {
                        case "58367":
                            strAreaName = "中心城区";
                            break;
                        case "58370":
                            strAreaName = "浦东新区";
                            break;
                        case "58361":
                            strAreaName = "闵行区";
                            break;
                        case "58362":
                            strAreaName = "宝山区";
                            break;
                        case "58462":
                            strAreaName = "松江区";
                            break;
                        case "58460":
                            strAreaName = "金山区";
                            break;
                        case "58461":
                            strAreaName = "青浦区";
                            break;
                        case "58463":
                            strAreaName = "奉贤区";
                            break;
                        case "58365":
                            strAreaName = "嘉定区";
                            break;
                        case "58366":
                            strAreaName = "崇明县";
                            break;
                        default:
                            strAreaName = "中心城区";
                            break;
                    }
                    strPM25 = strSingleArea[1];

                    switch (strSingleArea[2])
                    {
                        case "一级":
                            strAQIQuaLevel = "1";
                            break;
                        case "二级":
                            strAQIQuaLevel = "2";
                            break;
                        case "三级":
                            strAQIQuaLevel = "3";
                            break;
                        case "四级":
                            strAQIQuaLevel = "4";
                            break;
                        case "五级":
                            strAQIQuaLevel = "5";
                            break;
                        case "六级":
                            strAQIQuaLevel = "6";
                            break;
                        default:
                            strAQIQuaLevel = "1";
                            break;
                    }

                    switch (strSingleArea[3])
                    {
                        case "一级":
                            strAirPolConLevel = "1";
                            break;
                        case "二级":
                            strAirPolConLevel = "2";
                            break;
                        case "三级":
                            strAirPolConLevel = "3";
                            break;
                        case "四级":
                            strAirPolConLevel = "4";
                            break;
                        case "五级":
                            strAirPolConLevel = "5";
                            break;
                        case "六级":
                            strAirPolConLevel = "6";
                            break;
                        default:
                            strAirPolConLevel = "1";
                            break;
                    }
                    if (i == 0)
                    {
                        sb.Append(string.Format(" SELECT '{0}','{1}','{2}','{3}','{4}','{5}'", strAreaName, strPM25, strAQIQuaLevel, strAirPolConLevel, strForecastDate, strPeriod));
                    }
                    else
                    {
                        sb.Append(string.Format(" UNION ALL SELECT '{0}','{1}','{2}','{3}','{4}','{5}'", strAreaName, strPM25, strAQIQuaLevel, strAirPolConLevel, strForecastDate, strPeriod));

                    }
                }
                m_Database.Execute("delete from T_AirMeto where ForecastDate='" + strForecastDate + "'");
                m_Database.Execute(sb.ToString());
                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "1", "2");
                return "success";
            }
            return "fail";
        }

        //保存数据并生成图片
        public string SaveAirPollutionForecastAndDrawMap(string data, string strForecastDate, string strPeriod, string hourType)
        {
            string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
            string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
            string functionName = "AirPollutionForecast_07";
            if (hourType == "07")
            {
                strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 07:00:00.000");
                strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 08:00:00.000");
                functionName = "AirPollutionForecast_07";
            }
            else if (hourType == "17")
            {
                strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                functionName = "AirPollutionForecast_17";
            }

            DateTime dtNow = DateTime.Now;
            strForecastDate = dtNow.ToString("yyyy-MM-dd 00:00:00");

            //if (IsPublished(functionName, strRcdTime) == true)
            //{
            //    return "published";
            //}

            //区域名称
            string strAreaName = "null";
            string strPM25 = "null";
            string strAQIQuaLevel = "null";
            string strAirPolConLevel = "null";
            StringBuilder sb = new StringBuilder("INSERT INTO T_AirMeto (Area, [PM2.5], AirQ,AirPo,ForecastDate,PERIOD)");
            if (data != "")
            {
                //data = data.TrimEnd(',');
                string[] cells = data.Split('&');
                Dictionary<string, string> AreaData = new Dictionary<string, string>();
                for (int i = 0; i < cells.Length; i++)
                {

                    string[] strSingleArea = cells[i].Split('_');

                    switch (strSingleArea[0])
                    {
                        case "58367":
                            strAreaName = "中心城区";
                            break;
                        case "58370":
                            strAreaName = "浦东新区";
                            break;
                        case "58361":
                            strAreaName = "闵行区";
                            break;
                        case "58362":
                            strAreaName = "宝山区";
                            break;
                        case "58462":
                            strAreaName = "松江区";
                            break;
                        case "58460":
                            strAreaName = "金山区";
                            break;
                        case "58461":
                            strAreaName = "青浦区";
                            break;
                        case "58463":
                            strAreaName = "奉贤区";
                            break;
                        case "58365":
                            strAreaName = "嘉定区";
                            break;
                        case "58366":
                            strAreaName = "崇明县";
                            break;
                        default:
                            strAreaName = "中心城区";
                            break;
                    }
                    strPM25 = strSingleArea[1];

                    switch (strSingleArea[2])
                    {
                        case "一级":
                            strAQIQuaLevel = "1";
                            break;
                        case "二级":
                            strAQIQuaLevel = "2";
                            break;
                        case "三级":
                            strAQIQuaLevel = "3";
                            break;
                        case "四级":
                            strAQIQuaLevel = "4";
                            break;
                        case "五级":
                            strAQIQuaLevel = "5";
                            break;
                        case "六级":
                            strAQIQuaLevel = "6";
                            break;
                        default:
                            strAQIQuaLevel = "1";
                            break;
                    }

                    switch (strSingleArea[3])
                    {
                        case "一级":
                            strAirPolConLevel = "1";
                            break;
                        case "二级":
                            strAirPolConLevel = "2";
                            break;
                        case "三级":
                            strAirPolConLevel = "3";
                            break;
                        case "四级":
                            strAirPolConLevel = "4";
                            break;
                        case "五级":
                            strAirPolConLevel = "5";
                            break;
                        case "六级":
                            strAirPolConLevel = "6";
                            break;
                        default:
                            strAirPolConLevel = "1";
                            break;
                    }
                    AreaData.Add(strAreaName, strAirPolConLevel);
                    if (i == 0)
                    {
                        sb.Append(string.Format(" SELECT '{0}','{1}','{2}','{3}','{4}','{5}'", strAreaName, strPM25, strAQIQuaLevel, strAirPolConLevel, strForecastDate, strPeriod));
                    }
                    else
                    {
                        sb.Append(string.Format(" UNION ALL SELECT '{0}','{1}','{2}','{3}','{4}','{5}'", strAreaName, strPM25, strAQIQuaLevel, strAirPolConLevel, strForecastDate, strPeriod));

                    }
                }
                m_Database.Execute("delete from T_AirMeto where ForecastDate='" + strForecastDate + "'");
                m_Database.Execute(sb.ToString());
                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "1", "2");

                string strExMapBaseUrl = ConfigurationManager.AppSettings["ExportMapURL"].ToString();

                string strMapName = "YYYYMMDDHH_YyYyMmDdHh_diffusion_sh_mmdd.GIF";
                //strMapName = strMapName.Replace("YYYYMMDDHH", DateTime.Now.AddDays(-2).ToString("yyyyMMdd20"));
                DateTime pubTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 20:00:00"));
                if (hourType == "07")
                {
                    strMapName = strMapName.Replace("YYYYMMDDHH", DateTime.Now.AddDays(-2).ToString("yyyyMMdd20"));
                    strMapName = strMapName.Replace("YyYyMmDdHh", DateTime.Now.ToString("yyyyMMdd08"));
                    strMapName = strMapName.Replace("mmdd", DateTime.Now.ToString("MMdd"));
                    pubTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 08:00:00"));
                }
                else if (hourType == "17")
                {
                    strMapName = strMapName.Replace("YYYYMMDDHH", DateTime.Now.AddDays(-1).ToString("yyyyMMdd20"));
                    strMapName = strMapName.Replace("YyYyMmDdHh", DateTime.Now.ToString("yyyyMMdd20"));
                    strMapName = strMapName.Replace("mmdd", DateTime.Now.AddDays(1).ToString("MMdd"));
                    //strMapName = strMapName.Replace("YyYyMmDdHh", DateTime.Now.AddDays(1).ToString("yyyyMMdd08"));
                    //pubTime = Convert.ToDateTime(DateTime.Now.AddDays(1).ToString("yyyy-MM-dd 20:00:00"));
                    pubTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 20:00:00"));
                }

                //strMapName = strMapName.Replace("mmdd", DateTime.Now.AddDays(1).ToString("MMdd"));
                Dictionary<string, string> ReAreaData = new Dictionary<string, string>();
                string[] reorderAreas = { "嘉定区", "浦东新区", "金山区", "崇明县", "中心城区", "奉贤区", "宝山区", "闵行区", "青浦区", "松江区" };
                for (int i = 0; i < reorderAreas.Length; i++)
                {
                    ReAreaData.Add(reorderAreas[i], AreaData[reorderAreas[i]]);
                }

                CreateIMG createImg = new CreateIMG(ReAreaData, pubTime, strMapName, strExMapBaseUrl);
                //生成图片保存的路径
                string imgPath = createImg.DealData();
                imgPath = imgPath.Replace("/", "\\");
                return "success" + "&" + imgPath;
            }
            return "fail";
        }

        //获取空气污染气象条件的历史数据
        public string LoadAirPollutionForecastHistoryCopy()
        {
            //正式语句
            string strSQL = String.Format("select Area, [PM2.5], AirQ,AirPo FROM T_AirMeto WHERE ForecastDate=(select MAX(ForecastDate) from T_AirMeto)");
            StringBuilder sb = new StringBuilder();
            DataSet ds = m_Database.GetDataset(strSQL);

            if (ds.Tables.Count > 0)
            {
                string strSingle = "";
                string strAreaName;
                string strAreaID;
                string strAQIQ_ID = "";
                string strAQIPo = "";
                for (int i = 0; i < ds.Tables.Count; i++)
                {
                    DataTable dTable = ds.Tables[i];

                    //生成实况，综合预报，模式数据的json
                    if (dTable.Rows.Count > 0)
                    {
                        for (int j = 0; j < dTable.Rows.Count; j++)
                        {
                            strAreaName = dTable.Rows[j]["Area"].ToString();
                            switch (strAreaName)
                            {
                                case "中心城区":
                                    strAreaID = "58367";
                                    break;
                                case "浦东新区":
                                    strAreaID = "58370";
                                    break;
                                case "闵行区":
                                    strAreaID = "58361";
                                    break;
                                case "宝山区":
                                    strAreaID = "58362";
                                    break;
                                case "松江区":
                                    strAreaID = "58462";
                                    break;
                                case "金山区":
                                    strAreaID = "58460";
                                    break;
                                case "青浦区":
                                    strAreaID = "58461";
                                    break;
                                case "奉贤区":
                                    strAreaID = "58463";
                                    break;
                                case "嘉定区":
                                    strAreaID = "58365";
                                    break;
                                case "崇明县":
                                    strAreaID = "58366";
                                    break;
                                default:
                                    strAreaID = "58367";
                                    break;
                            }

                            switch (dTable.Rows[j]["AirQ"].ToString())
                            {
                                case "1":
                                    strAQIQ_ID = "一级";
                                    break;
                                case "2":
                                    strAQIQ_ID = "二级";
                                    break;
                                case "3":
                                    strAQIQ_ID = "三级";
                                    break;
                                case "4":
                                    strAQIQ_ID = "四级";
                                    break;
                                case "5":
                                    strAQIQ_ID = "五级";
                                    break;
                                case "6":
                                    strAQIQ_ID = "六级";
                                    break;
                                default:
                                    strAQIQ_ID = "一级";
                                    break;
                            }

                            switch (dTable.Rows[j]["AirPo"].ToString())
                            {
                                case "1":
                                    strAQIPo = "一级";
                                    break;
                                case "2":
                                    strAQIPo = "二级";
                                    break;
                                case "3":
                                    strAQIPo = "三级";
                                    break;
                                case "4":
                                    strAQIPo = "四级";
                                    break;
                                case "5":
                                    strAQIPo = "五级";
                                    break;
                                case "6":
                                    strAQIPo = "六级";
                                    break;
                                default:
                                    strAQIPo = "一级";
                                    break;
                            }
                            sb.Append("{\"area\":\"" + strAreaID + "\",\"PM25\":\"" + dTable.Rows[i]["PM2.5"].ToString() + "\",\"AirQ\":\"" + strAQIQ_ID + "\",\"AirPo\":\"" + strAQIPo + "\"},");
                        }

                    }
                }

            }
            return "[" + sb.ToString().Trim(',') + "]";
        }

        public string LoadAirPollutionForecastHistory()
        {
            //正式语句
            string strSQL = String.Format("select Area, [PM2.5], AirQ,AirPo FROM T_AirMeto WHERE ForecastDate=(select MAX(ForecastDate) from T_AirMeto)");
            StringBuilder sb = new StringBuilder();
            DataSet ds = m_Database.GetDataset(strSQL);

            if (ds.Tables.Count > 0)
            {
                string strSingle = "";
                string strAreaName;
                string strAreaID;
                string strAQIQ_ID = "";
                string strAQIPo = "";
                string strPM25 = "";
                //存储PM2.5值的json序列
                StringBuilder strPM25Json = new StringBuilder("{");
                //存储空气质量等级的json序列
                StringBuilder strAirQuaJson = new StringBuilder("{");
                //存储空气污染气象条件的json序列
                StringBuilder strAirPolLevelJson = new StringBuilder("{");
                //存储颜色编码的json序列
                StringBuilder strAirPolLevelColorJson = new StringBuilder("{");

                for (int i = 0; i < ds.Tables.Count; i++)
                {
                    DataTable dTable = ds.Tables[i];

                    //生成实况，综合预报，模式数据的json
                    if (dTable.Rows.Count > 0)
                    {
                        for (int j = 0; j < dTable.Rows.Count; j++)
                        {
                            strAreaName = dTable.Rows[j]["Area"].ToString();
                            strPM25 = dTable.Rows[j]["PM2.5"].ToString();
                            switch (strAreaName)
                            {
                                case "中心城区":
                                    strAreaID = "58367";
                                    break;
                                case "浦东新区":
                                    strAreaID = "58370";
                                    break;
                                case "闵行区":
                                    strAreaID = "58361";
                                    break;
                                case "宝山区":
                                    strAreaID = "58362";
                                    break;
                                case "松江区":
                                    strAreaID = "58462";
                                    break;
                                case "金山区":
                                    strAreaID = "58460";
                                    break;
                                case "青浦区":
                                    strAreaID = "58461";
                                    break;
                                case "奉贤区":
                                    strAreaID = "58463";
                                    break;
                                case "嘉定区":
                                    strAreaID = "58365";
                                    break;
                                case "崇明县":
                                    strAreaID = "58366";
                                    break;
                                default:
                                    strAreaID = "58367";
                                    break;
                            }

                            switch (dTable.Rows[j]["AirQ"].ToString())
                            {
                                case "1":
                                    strAQIQ_ID = "一级";
                                    break;
                                case "2":
                                    strAQIQ_ID = "二级";
                                    break;
                                case "3":
                                    strAQIQ_ID = "三级";
                                    break;
                                case "4":
                                    strAQIQ_ID = "四级";
                                    break;
                                case "5":
                                    strAQIQ_ID = "五级";
                                    break;
                                case "6":
                                    strAQIQ_ID = "六级";
                                    break;
                                default:
                                    strAQIQ_ID = "一级";
                                    break;
                            }

                            switch (dTable.Rows[j]["AirPo"].ToString())
                            {
                                case "1":
                                    strAQIPo = "一级";
                                    break;
                                case "2":
                                    strAQIPo = "二级";
                                    break;
                                case "3":
                                    strAQIPo = "三级";
                                    break;
                                case "4":
                                    strAQIPo = "四级";
                                    break;
                                case "5":
                                    strAQIPo = "五级";
                                    break;
                                case "6":
                                    strAQIPo = "六级";
                                    break;
                                default:
                                    strAQIPo = "一级";
                                    break;
                            }

                            strPM25Json.Append("\"" + strAreaID + "_Value\":\"" + strPM25 + "\",");
                            strAirQuaJson.Append("\"" + strAreaID + "_airQua\":\"" + strAQIQ_ID + "\",");
                            strAirPolLevelJson.Append("\"" + strAreaID + "_aqiPolCon\":\"" + strAQIPo + "\",");
                            strAirPolLevelColorJson.Append("\"" + strAreaID + "\":\"" + dTable.Rows[j]["AirPo"].ToString() + "\",");
                        }

                        if (strPM25Json.Length > 1)
                        {
                            strPM25Json.Remove(strPM25Json.Length - 1, 1);
                            strPM25Json.Append("}");
                        }
                        if (strAirQuaJson.Length > 1)
                        {
                            strAirQuaJson.Remove(strAirQuaJson.Length - 1, 1);
                            strAirQuaJson.Append("}");
                        }
                        if (strAirPolLevelJson.Length > 1)
                        {
                            strAirPolLevelJson.Remove(strAirPolLevelJson.Length - 1, 1);
                            strAirPolLevelJson.Append("}");
                        }
                        if (strAirPolLevelColorJson.Length > 1)
                        {
                            strAirPolLevelColorJson.Remove(strAirPolLevelColorJson.Length - 1, 1);
                            strAirPolLevelColorJson.Append("}");
                        }

                        sb.Append("{");
                        sb.Append("\"PM25\":" + strPM25Json.ToString() + ",");
                        sb.Append("\"AirQua\":" + strAirQuaJson.ToString() + ",");
                        sb.Append("\"Color\":" + strAirPolLevelColorJson.ToString() + ",");
                        sb.Append("\"AirPolLevel\":" + strAirPolLevelJson.ToString() + "}");
                    }
                }

            }
            return sb.ToString(); ;
        }

        //获取AQI分区的历史数据
        public string LoadAQIAreaHistoryCopy()
        {
            //正式语句
            string strSQL = String.Format("select area, grade, AQI,itemid,haze FROM T_AQIArea WHERE ForecastDate=(select MAX(ForecastDate) from T_AQIArea)");
            StringBuilder sb = new StringBuilder();
            DataSet ds = m_Database.GetDataset(strSQL);

            if (ds.Tables.Count > 0)
            {
                string strAreaName;
                string strAreaID;
                string strAQIGrade = "";
                string strAQI = "";
                string strHaze = "";
                string strItemName = "";
                for (int i = 0; i < ds.Tables.Count; i++)
                {
                    DataTable dTable = ds.Tables[i];

                    //生成实况，综合预报，模式数据的json
                    if (dTable.Rows.Count > 0)
                    {
                        for (int j = 0; j < dTable.Rows.Count; j++)
                        {
                            strAreaName = dTable.Rows[j]["area"].ToString();
                            switch (strAreaName)
                            {
                                case "中心城区":
                                    strAreaID = "58367";
                                    break;
                                case "浦东新区":
                                    strAreaID = "58370";
                                    break;
                                case "闵行区":
                                    strAreaID = "58361";
                                    break;
                                case "宝山区":
                                    strAreaID = "58362";
                                    break;
                                case "松江区":
                                    strAreaID = "58462";
                                    break;
                                case "金山区":
                                    strAreaID = "58460";
                                    break;
                                case "青浦区":
                                    strAreaID = "58461";
                                    break;
                                case "奉贤区":
                                    strAreaID = "58463";
                                    break;
                                case "嘉定区":
                                    strAreaID = "58365";
                                    break;
                                case "崇明县":
                                    strAreaID = "58366";
                                    break;
                                default:
                                    strAreaID = "58367";
                                    break;
                            }

                            switch (dTable.Rows[j]["grade"].ToString())
                            {
                                case "1":
                                    strAQIGrade = "一级";
                                    break;
                                case "2":
                                    strAQIGrade = "二级";
                                    break;
                                case "3":
                                    strAQIGrade = "三级";
                                    break;
                                case "4":
                                    strAQIGrade = "四级";
                                    break;
                                case "5":
                                    strAQIGrade = "五级";
                                    break;
                                case "6":
                                    strAQIGrade = "六级";
                                    break;
                                default:
                                    strAQIGrade = "一级";
                                    break;
                            }
                            strAQI = dTable.Rows[j]["AQI"].ToString();

                            switch (dTable.Rows[j]["haze"].ToString())
                            {
                                case "1":
                                    strHaze = "无霾";
                                    break;
                                case "2":
                                    strHaze = "轻微霾";
                                    break;
                                case "3":
                                    strHaze = "轻度霾";
                                    break;
                                case "4":
                                    strHaze = "中度霾";
                                    break;
                                case "5":
                                    strHaze = "重度霾";
                                    break;
                                case "6":
                                    strHaze = "严重霾";
                                    break;
                                default:
                                    strHaze = "无霾";
                                    break;
                            }
                            switch (dTable.Rows[j]["itemid"].ToString())
                            {
                                case "1":
                                    strItemName = "PM2.5";
                                    break;
                                case "2":
                                    strItemName = "PM10";
                                    break;
                                case "3":
                                    strItemName = "O3-1小时";
                                    break;
                                case "4":
                                    strItemName = "O3-8小时";
                                    break;
                                case "5":
                                    strItemName = "CO";
                                    break;
                                case "6":
                                    strItemName = "SO2小时";
                                    break;
                                case "7":
                                    strItemName = "NO2";
                                    break;
                                default:
                                    strItemName = "PM2.5";
                                    break;
                            }

                            sb.Append("{\"area\":\"" + strAreaID + "\",\"grade\":\"" + strAQIGrade + "\",\"item\":\"" + strItemName + "\",\"AQI\":\"" + strAQI + "\",\"haze\":\"" + strHaze + "\"},");
                        }

                    }
                }

            }
            return "[" + sb.ToString().Trim(',') + "]";
        }

        public string LoadAQIAreaHistory()
        {
            //正式语句
            string strSQL = String.Format("select area, grade, AQI,itemid,haze FROM T_AQIArea WHERE ForecastDate=(select MAX(ForecastDate) from T_AQIArea)");
            StringBuilder sb = new StringBuilder();
            DataSet ds = m_Database.GetDataset(strSQL);

            StringBuilder strPolLevelJson = new StringBuilder("{");
            //存储首要污染物的json序列
            StringBuilder strFirstPolJson = new StringBuilder("{");
            //存储AQIjson序列
            StringBuilder strAQIJson = new StringBuilder("{");
            //存储AQI的序列
            StringBuilder strHazeJson = new StringBuilder("{");
            //存储污染等级颜色json序列
            StringBuilder strColorJson = new StringBuilder("{");


            if (ds.Tables.Count > 0)
            {
                string strAreaName;
                string strAreaID;
                string strAQIGrade = "";
                string strAQI = "";
                string strHaze = "";
                string strItemName = "";
                for (int i = 0; i < ds.Tables.Count; i++)
                {
                    DataTable dTable = ds.Tables[i];
                    //生成实况，综合预报，模式数据的json
                    if (dTable.Rows.Count > 0)
                    {
                        for (int j = 0; j < dTable.Rows.Count; j++)
                        {
                            strAreaName = dTable.Rows[j]["area"].ToString();
                            switch (strAreaName)
                            {
                                case "中心城区":
                                    strAreaID = "58367";
                                    break;
                                case "浦东新区":
                                    strAreaID = "58370";
                                    break;
                                case "闵行区":
                                    strAreaID = "58361";
                                    break;
                                case "宝山区":
                                    strAreaID = "58362";
                                    break;
                                case "松江区":
                                    strAreaID = "58462";
                                    break;
                                case "金山区":
                                    strAreaID = "58460";
                                    break;
                                case "青浦区":
                                    strAreaID = "58461";
                                    break;
                                case "奉贤区":
                                    strAreaID = "58463";
                                    break;
                                case "嘉定区":
                                    strAreaID = "58365";
                                    break;
                                case "崇明县":
                                    strAreaID = "58366";
                                    break;
                                default:
                                    strAreaID = "58367";
                                    break;
                            }

                            switch (dTable.Rows[j]["grade"].ToString())
                            {
                                case "1":
                                    strAQIGrade = "优";
                                    break;
                                case "2":
                                    strAQIGrade = "良";
                                    break;
                                case "3":
                                    strAQIGrade = "轻度污染";
                                    break;
                                case "4":
                                    strAQIGrade = "中度污染";
                                    break;
                                case "5":
                                    strAQIGrade = "重度污染";
                                    break;
                                case "6":
                                    strAQIGrade = "严重污染";
                                    break;
                                default:
                                    strAQIGrade = "良";
                                    break;
                            }
                            strAQI = dTable.Rows[j]["AQI"].ToString();

                            switch (dTable.Rows[j]["haze"].ToString())
                            {
                                case "1":
                                    strHaze = "无霾";
                                    break;
                                case "2":
                                    strHaze = "轻微霾";
                                    break;
                                case "3":
                                    strHaze = "轻度霾";
                                    break;
                                case "4":
                                    strHaze = "中度霾";
                                    break;
                                case "5":
                                    strHaze = "重度霾";
                                    break;
                                case "6":
                                    strHaze = "严重霾";
                                    break;
                                default:
                                    strHaze = "无霾";
                                    break;
                            }
                            switch (dTable.Rows[j]["itemid"].ToString())
                            {
                                case "6":
                                    strItemName = "PM2.5";
                                    break;
                                case "3":
                                    strItemName = "PM10";
                                    break;
                                case "5":
                                    strItemName = "O3-1小时";
                                    break;
                                case "4":
                                    strItemName = "CO";
                                    break;
                                case "1":
                                    strItemName = "SO2小时";
                                    break;
                                case "2":
                                    strItemName = "NO2";
                                    break;
                                default:
                                    strItemName = "PM2.5";
                                    break;
                            }

                            strPolLevelJson.Append("\"" + strAreaID + "_Level\":\"" + strAQIGrade + "\",");
                            strFirstPolJson.Append("\"" + strAreaID + "_Item\":\"" + strItemName + "\",");
                            strAQIJson.Append("\"" + strAreaID + "_AQI\":\"" + strAQI + "\",");
                            strColorJson.Append("\"" + strAreaID + "\":\"" + dTable.Rows[j]["grade"].ToString() + "\",");
                            strHazeJson.Append("\"" + strAreaID + "\":\"" + strHaze + "\",");
                        }

                        if (strPolLevelJson.Length > 1)
                        {
                            strPolLevelJson.Remove(strPolLevelJson.Length - 1, 1);
                            strPolLevelJson.Append("}");
                        }
                        if (strFirstPolJson.Length > 1)
                        {
                            strFirstPolJson.Remove(strFirstPolJson.Length - 1, 1);
                            strFirstPolJson.Append("}");
                        }
                        if (strAQIJson.Length > 1)
                        {
                            strAQIJson.Remove(strAQIJson.Length - 1, 1);
                            strAQIJson.Append("}");
                        }
                        if (strColorJson.Length > 1)
                        {
                            strColorJson.Remove(strColorJson.Length - 1, 1);
                            strColorJson.Append("}");
                        }
                        if (strHazeJson.Length > 1)
                        {
                            strHazeJson.Remove(strHazeJson.Length - 1, 1);
                            strHazeJson.Append("}");
                        }

                        sb.Append("{");
                        sb.Append("\"PolLevel\":" + strPolLevelJson.ToString() + ",");
                        sb.Append("\"FirstPol\":" + strFirstPolJson.ToString() + ",");
                        sb.Append("\"AQI\":" + strAQIJson.ToString() + ",");
                        sb.Append("\"LevelColor\":" + strColorJson.ToString() + ",");
                        sb.Append("\"HazLevel\":" + strHazeJson.ToString() + "}");

                    }
                }

            }
            return sb.ToString();
        }

        //获取一张表内最大的ForecastDate
        public string GetMaxForecastDate(string tableName)
        {
            string strMaxForecastDate = "";
            if (tableName != "")
            {
                string strSQL = "SELECT MAX(ForecastDate) FROM " + tableName;
                DataTable dt = m_Database.GetDataTable(strSQL);
                if (dt.Rows.Count > 0)
                {
                    strMaxForecastDate = dt.Rows[dt.Rows.Count - 1][0].ToString();
                }
            }
            return strMaxForecastDate;
        }

        //获取重点城市预报的数据


        public string GetMainCityForecastDataCopy()
        {
            string strTextPath = ConfigurationManager.AppSettings["MainCityTextPath"];
            string strFileName = "AQI_major_" + DateTime.Now.AddDays(-1).ToString("yyyyMMdd") + "20.txt";
            string str;
            string strContent = "";
            StreamReader sr = new StreamReader(strTextPath + strFileName, false);
            //标记读取的行数
            int intReadLineIndex = 0;

            string strFirstPolLine = "";
            string strAQILine = "";
            string strKQWRLine = "";
            string strHazeLine = "";
            str = sr.ReadLine();
            while (str != null)
            {
                intReadLineIndex++;
                //首要污染物
                if (intReadLineIndex == 3)
                {
                    strFirstPolLine = str;
                }
                //AQI
                else if (intReadLineIndex == 4)
                {
                    strAQILine = str;
                }
                //空气污染气象条件
                else if (intReadLineIndex == 5)
                {
                    strKQWRLine = str;
                }
                //霾级别
                else if (intReadLineIndex == 6)
                {
                    strHazeLine = str;
                }
                strContent += str;
                str = sr.ReadLine();
            }
            sr.Close();
            string[] firstPolList = null;
            string[] aqiList = null;
            string[] kqwrList = null;
            string[] hazeList = null;
            //根据AQI计算污染等级
            string strPolLevel = "";
            if (strFirstPolLine != "")
            {
                string strTrimFirstPolLine = Regex.Replace(strFirstPolLine, @"[\s ]+", ",");
                firstPolList = strTrimFirstPolLine.Split(',');
            }
            if (strAQILine != "")
            {
                string strTrimAQILine = Regex.Replace(strAQILine, @"[\s ]+", ",");
                aqiList = strTrimAQILine.Split(',');
            }
            if (strKQWRLine != "")
            {
                string strTrimKQWRLine = Regex.Replace(strKQWRLine, @"[\s ]+", ",");
                kqwrList = strTrimKQWRLine.Split(',');
            }
            if (strHazeLine != "")
            {
                string strTrimHazeLine = Regex.Replace(strHazeLine, @"[\s ]+", ",");
                hazeList = strTrimHazeLine.Split(',');
            }
            StringBuilder sb = new StringBuilder();
            if (firstPolList.Length > 0 && aqiList.Length > 0 && kqwrList.Length > 0 && hazeList.Length > 0)
                for (int i = 0; i < 11; i++)
                {
                    string strCityName = "";
                    switch (i)
                    {
                        case 0:
                            strCityName = "Shanghai";
                            break;
                        case 1:
                            strCityName = "Nanjing";
                            break;
                        case 2:
                            strCityName = "Suzhou";
                            break;
                        case 3:
                            strCityName = "Hangzhou";
                            break;
                        case 4:
                            strCityName = "Ningbo";
                            break;
                        case 5:
                            strCityName = "Hefei";
                            break;
                        case 6:
                            strCityName = "Fuzhou";
                            break;
                        case 7:
                            strCityName = "Xiamen";
                            break;
                        case 8:
                            strCityName = "Nanchang";
                            break;
                        case 9:
                            strCityName = "Jinan";
                            break;
                        case 10:
                            strCityName = "Qingdao";
                            break;
                    }
                    strPolLevel = CalculateAQLLevel(aqiList[i + 2]);
                    sb.Append("\"FirstItem_" + strCityName + "\":\"" + firstPolList[i + 2] + "\",\"" + "AQI_" + strCityName + "\":\"" + aqiList[i + 2] + "\",\"" + "AirPolLevel_" + strCityName + "\":\"" + kqwrList[i + 2] + "\",\"" + "Haze_" + strCityName + "\":\"" + hazeList[i + 2] + "\",\"" + "PolLevel_" + strCityName + "\":\"" + strPolLevel + "\"" + ",");
                }
            return "{" + sb.ToString().Trim(',') + "}";
        }

        public string GetMainCityForecastData()
        {
            string strTextPath = ConfigurationManager.AppSettings["MainCityTextPath"];
            string strFileName = "AQI_major_" + DateTime.Now.AddDays(-1).ToString("yyyyMMdd") + "20.txt";
            string str;
            string strContent = "";
            if (File.Exists(strTextPath + strFileName))
            {

                StreamReader sr = new StreamReader(strTextPath + strFileName, false);
                //标记读取的行数
                int intReadLineIndex = 0;

                string strFirstPolLine = "";
                string strAQILine = "";
                string strKQWRLine = "";
                string strHazeLine = "";
                str = sr.ReadLine();
                while (str != null)
                {
                    intReadLineIndex++;
                    //首要污染物
                    if (intReadLineIndex == 3)
                    {
                        strFirstPolLine = str;
                    }
                    //AQI
                    else if (intReadLineIndex == 4)
                    {
                        strAQILine = str;
                    }
                    //空气污染气象条件
                    else if (intReadLineIndex == 5)
                    {
                        strKQWRLine = str;
                    }
                    //霾级别
                    else if (intReadLineIndex == 6)
                    {
                        strHazeLine = str;
                    }
                    strContent += str;
                    str = sr.ReadLine();
                }
                sr.Close();
                string[] firstPolList = null;
                string[] aqiList = null;
                string[] kqwrList = null;
                string[] hazeList = null;
                //根据AQI计算污染等级
                string strPolLevel = "";
                if (strFirstPolLine != "")
                {
                    string strTrimFirstPolLine = Regex.Replace(strFirstPolLine, @"[\s ]+", ",");
                    firstPolList = strTrimFirstPolLine.Split(',');
                }
                if (strAQILine != "")
                {
                    string strTrimAQILine = Regex.Replace(strAQILine, @"[\s ]+", ",");
                    aqiList = strTrimAQILine.Split(',');
                }
                if (strKQWRLine != "")
                {
                    string strTrimKQWRLine = Regex.Replace(strKQWRLine, @"[\s ]+", ",");
                    kqwrList = strTrimKQWRLine.Split(',');
                }
                if (strHazeLine != "")
                {
                    string strTrimHazeLine = Regex.Replace(strHazeLine, @"[\s ]+", ",");
                    hazeList = strTrimHazeLine.Split(',');
                }
                StringBuilder sb = new StringBuilder();
                if (firstPolList.Length > 0 && aqiList.Length > 0 && kqwrList.Length > 0 && hazeList.Length > 0)
                {
                    string strAirPolLevel = "";
                    string strHazeLevel = "";
                    for (int i = 0; i < 11; i++)
                    {
                        string strCityName = "";
                        switch (i)
                        {
                            case 0:
                                strCityName = "Shanghai";
                                break;
                            case 1:
                                strCityName = "Nanjing";
                                break;
                            case 2:
                                strCityName = "Suzhou";
                                break;
                            case 3:
                                strCityName = "Hangzhou";
                                break;
                            case 4:
                                strCityName = "Ningbo";
                                break;
                            case 5:
                                strCityName = "Hefei";
                                break;
                            case 6:
                                strCityName = "Fuzhou";
                                break;
                            case 7:
                                strCityName = "Xiamen";
                                break;
                            case 8:
                                strCityName = "Nanchang";
                                break;
                            case 9:
                                strCityName = "Jinan";
                                break;
                            case 10:
                                strCityName = "Qingdao";
                                break;
                        }

                        switch (kqwrList[i + 2])
                        {
                            case "1":
                                strAirPolLevel = "一级";
                                break;
                            case "2":
                                strAirPolLevel = "二级";
                                break;
                            case "3":
                                strAirPolLevel = "三级";
                                break;
                            case "4":
                                strAirPolLevel = "四级";
                                break;
                            case "5":
                                strAirPolLevel = "五级";
                                break;
                            default:
                                strAirPolLevel = "一级";
                                break;
                        }
                        switch (hazeList[i + 2])
                        {
                            case "":
                                strHazeLevel = "无霾";
                                break;
                            case "1":
                                strHazeLevel = "轻微霾";
                                break;
                            case "2":
                                strHazeLevel = "轻度霾";
                                break;
                            case "3":
                                strHazeLevel = "中度霾";
                                break;
                            case "4":
                                strHazeLevel = "重度霾";
                                break;
                            case "5":
                                strHazeLevel = "严重霾";
                                break;
                            default:
                                strHazeLevel = "无霾";
                                break;
                        }
                        strPolLevel = CalculateAQLLevel(aqiList[i + 2]);
                        sb.Append("\"FirstItem_" + strCityName + "\":\"" + firstPolList[i + 2] + "\",\"" + "AQI_" + strCityName + "\":\"" + aqiList[i + 2] + "\",\"" + "AirPolLevel_" + strCityName + "\":\"" + strAirPolLevel + "\",\"" + "Haze_" + strCityName + "\":\"" + strHazeLevel + "\",\"" + "PolLevel_" + strCityName + "\":\"" + strPolLevel + "\"" + ",");
                    }
                }
                return "{" + sb.ToString().Trim(',') + "}";
            }
            else
            {
                return "fail";
            }
        }

        public string GetPublishLogData(string start, string limit)
        {
            int intStart = Convert.ToInt32(start);
            int intLimit = Convert.ToInt32(limit);
            string strJson = "";
            //存储所有发布记录的集合
            List<string> totalList = new List<string>();

            string strSQL = "SELECT * FROM T_ProductLog order by StartTime desc";
            //DataTable dt = m_Database.GetDataTable(strSQL);
            DataTable dt = m_DatabaseJX.GetDataTable(strSQL);
            StringBuilder sb = null;
            if (dt.Rows.Count > 0)
            {
                if (dt.Rows.Count > 0)
                {
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        sb = new StringBuilder();
                        sb.Append("{\"" + "ID" + "\":\"" + dt.Rows[i]["ID"].ToString() + "\",\""
                            + "ProductType" + "\":\"" + dt.Rows[i]["ProductType"].ToString() + "\",\""
                            + "ProductName" + "\":\"" + dt.Rows[i]["ProductName"].ToString() + "\",\""
                            + "ReleaseType" + "\":\"" + dt.Rows[i]["ReleaseType"].ToString() + "\",\""
                            + "StartTime" + "\":\"" + dt.Rows[i]["StartTime"].ToString() + "\",\""
                            + "EndTime" + "\":\"" + dt.Rows[i]["EndTime"].ToString() + "\",\""
                            + "State" + "\":\"" + dt.Rows[i]["State"].ToString() + "\",\""
                            + "Address" + "\":\"" + dt.Rows[i]["Address"].ToString() + "\",\""
                            + "User" + "\":\"" + dt.Rows[i]["User"].ToString() + "\",\""
                            + "IPAddress" + "\":\"" + dt.Rows[i]["IPAddress"].ToString() + "\",\""
                            //+ "Detail" + "\":\"" + dt.Rows[i]["Detail"].ToString() + "\",\""
                             + "Detail" + "\":\"" + "" + "\",\""
                             + "DeadLine" + "\":\"" + dt.Rows[i]["DeadLine"].ToString() + "\",\""
                              + "Type" + "\":\"" + dt.Rows[i]["Type"].ToString() + "\",\""
                              + "FileTempPath" + "\":\"" + dt.Rows[i]["FileTempPath"].ToString().Replace("\\", "&")
                            + "\"}");
                        totalList.Add(sb.ToString());
                    }
                    //string strLST = DateTime.Parse(dt.Rows[dt.Rows.Count - 1]["LST"].ToString()).ToString("yyyy年MM月dd日");
                    //sb.Append("{\"LST\":\"" + strLST + "\",\"O3\":\"" + dt.Rows[dt.Rows.Count - 1]["O3"].ToString() + "\",\"O38\":\"" + dt.Rows[dt.Rows.Count - 1]["O38"].ToString() + "\",\"O3Period\":\"" + dt.Rows[dt.Rows.Count - 1]["O3Period"].ToString() + "\",\"O38Period\":\"" + dt.Rows[dt.Rows.Count - 1]["O38Period"].ToString() + "\"},");
                }

                StringBuilder showData = new StringBuilder();
                int end = intStart + intLimit;
                int intUseEnd = end < totalList.Count ? end : totalList.Count;
                for (int i = intStart; i < intUseEnd; i++)
                {
                    showData.Append(totalList[i]);
                    if (i != end)
                    {
                        showData.Append(",");
                    }
                }

                //for (int i = 0; i < totalList.Count; i++)
                //{
                //    showData.Append(totalList[i]);
                //    if (i != totalList.Count-1)
                //    {
                //        showData.Append(",");
                //    }
                //}

                strJson = "{" +
           "\"metaData\":{" +
            " \"totalProperty\":\"results\"," +
             "\"root\":\"rows\"," +
             "\"id\":\"id\"," +
             "\"fields\":[" +
               "{\"name\":\"ProductType\",\"mapping\":\"ProductType\"}," +
               "{\"name\":\"ProductName\",\"mapping\":\"ProductName\"}," +
               "{\"name\":\"ReleaseType\",\"mapping\":\"ReleaseType\"}," +
               "{\"name\":\"StartTime\",\"mapping\":\"StartTime\"}," +
               "{\"name\":\"EndTime\",\"mapping\":\"EndTime\"}," +
               "{\"name\":\"State\",\"mapping\":\"State\"}," +
               "{\"name\":\"Address\",\"mapping\":\"Address\"}," +
               "{\"name\":\"User\",\"mapping\":\"User\"}," +
               "{\"name\":\"IPAddress\",\"mapping\":\"IPAddress\"}," +
               "{\"name\":\"Detail\",\"mapping\":\"Detail\"}," +
               "{\"name\":\"DeadLine\",\"mapping\":\"DeadLine\"}," +
               "{\"name\":\"Type\",\"mapping\":\"Type\"}," +
               "{\"name\":\"FileTempPath\",\"mapping\":\"FileTempPath\"}" +
             "]" +
           "}," +
           "\"results\":\"" + totalList.Count + "\",\"" +
           "rows\":[" +
            showData.ToString().TrimEnd(',') +
           "]" +
         "}";

            }
            return strJson;
        }

        public string GetPublishLogDataNew(string start, string limit,string startTime,string endTime,string productName,string pubMethod,string pubState,string user)
        {
            int intStart = Convert.ToInt32(start);
            int intLimit = Convert.ToInt32(limit);

            //起止时间语句部分
            string strTimeSQL = "";
            //产品名称语句部分
            string strProNameSQL = "";
            //发布方式语句部分
            string strMethodSQL = "";
            //发布状态语句部分
            string strStateSQL = "";
            //发布用户语句部分
            string sreUserSQL = "";
            List<string> sqlList=new List<string>();
            if (startTime != "" && endTime != "" && startTime != null && endTime != null)
            {
                DateTime dtStart = new DateTime(Convert.ToInt32(startTime.Split('-')[0]),Convert.ToInt32(startTime.Split('-')[1]),Convert.ToInt32(startTime.Split('-')[2]));
                DateTime dtEnd = new DateTime(Convert.ToInt32(endTime.Split('-')[0]),Convert.ToInt32(endTime.Split('-')[1]),Convert.ToInt32(endTime.Split('-')[2]));
                strTimeSQL = "datediff (dd, '" + dtStart.ToString("yyyy-MM-dd HH:mm:ss.000") + "'," + "StartTime) >=0 AND datediff (dd, '" + dtEnd.ToString("yyyy-MM-dd HH:mm:ss.000") + "',StartTime) <=0";
                sqlList.Add(strTimeSQL);
            }
            if (productName != "" && productName != null && productName != "全部")
            {
                strProNameSQL = "ProductType='" + productName+"'";
                sqlList.Add(strProNameSQL);
            }
            if (pubMethod != "" && pubMethod != null && pubMethod != "全部")
            {
                strMethodSQL = "ReleaseType='" + pubMethod + "'";
                sqlList.Add(strMethodSQL);
            }
            if (pubState != "" && pubState != null && pubState != "全部")
            {
                if (pubState == "发布成功")
                {
                    strStateSQL = "State='0'";
                }
                else if (pubState == "发布失败")
                {
                    strStateSQL = "State='1'";
                }
                sqlList.Add(strStateSQL);
            }
            if (user != "" && user != null)
            {
                sreUserSQL = "User='" + user + "'";
                sqlList.Add(sreUserSQL);
            }
            string strJson = "";
            //存储所有发布记录的集合
            List<string> totalList = new List<string>();
            string strSQL = "SELECT * FROM T_ProductLog order by StartTime desc";
            if (sqlList.Count == 0)
            {
                strSQL = "SELECT * FROM T_ProductLog order by StartTime desc";
            }
            else
            {
                strSQL = "SELECT * FROM T_ProductLog WHERE";
                for (int i = 0; i < sqlList.Count; i++)
                {
                    if (i < sqlList.Count - 1)
                    {
                        strSQL += " " + sqlList[i] + " AND";
                    }
                    else
                    {
                        strSQL += " " + sqlList[i]+" order by StartTime desc";
                    }
                }
            }
            

            //string strSQL = "SELECT * FROM T_ProductLog order by StartTime desc";
            //DataTable dt = m_Database.GetDataTable(strSQL);
            DataTable dt = m_DatabaseJX.GetDataTable(strSQL);
            StringBuilder sb = null;
            if (dt.Rows.Count > 0)
            {
                if (dt.Rows.Count > 0)
                {
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        sb = new StringBuilder();
                        sb.Append("{\"" + "ID" + "\":\"" + dt.Rows[i]["ID"].ToString() + "\",\""
                            + "ProductType" + "\":\"" + dt.Rows[i]["ProductType"].ToString() + "\",\""
                            + "ProductName" + "\":\"" + dt.Rows[i]["ProductName"].ToString() + "\",\""
                            + "ReleaseType" + "\":\"" + dt.Rows[i]["ReleaseType"].ToString() + "\",\""
                            + "StartTime" + "\":\"" + dt.Rows[i]["StartTime"].ToString() + "\",\""
                            + "EndTime" + "\":\"" + dt.Rows[i]["EndTime"].ToString() + "\",\""
                            + "State" + "\":\"" + dt.Rows[i]["State"].ToString() + "\",\""
                            + "Address" + "\":\"" + dt.Rows[i]["Address"].ToString() + "\",\""
                            + "User" + "\":\"" + dt.Rows[i]["User"].ToString() + "\",\""
                            + "IPAddress" + "\":\"" + dt.Rows[i]["IPAddress"].ToString() + "\",\""
                            //+ "Detail" + "\":\"" + dt.Rows[i]["Detail"].ToString() + "\",\""
                             + "Detail" + "\":\"" + "" + "\",\""
                             + "DeadLine" + "\":\"" + dt.Rows[i]["DeadLine"].ToString() + "\",\""
                              + "Type" + "\":\"" + dt.Rows[i]["Type"].ToString() + "\",\""
                              + "FileTempPath" + "\":\"" + dt.Rows[i]["FileTempPath"].ToString().Replace("\\", "&")
                            + "\"}");
                        totalList.Add(sb.ToString());
                    }
                    //string strLST = DateTime.Parse(dt.Rows[dt.Rows.Count - 1]["LST"].ToString()).ToString("yyyy年MM月dd日");
                    //sb.Append("{\"LST\":\"" + strLST + "\",\"O3\":\"" + dt.Rows[dt.Rows.Count - 1]["O3"].ToString() + "\",\"O38\":\"" + dt.Rows[dt.Rows.Count - 1]["O38"].ToString() + "\",\"O3Period\":\"" + dt.Rows[dt.Rows.Count - 1]["O3Period"].ToString() + "\",\"O38Period\":\"" + dt.Rows[dt.Rows.Count - 1]["O38Period"].ToString() + "\"},");
                }

                StringBuilder showData = new StringBuilder();
                int end = intStart + intLimit;
                int intUseEnd = end < totalList.Count ? end : totalList.Count;
                for (int i = intStart; i < intUseEnd; i++)
                {
                    showData.Append(totalList[i]);
                    if (i != end)
                    {
                        showData.Append(",");
                    }
                }
                strJson = "{" +
           "\"metaData\":{" +
            " \"totalProperty\":\"results\"," +
             "\"root\":\"rows\"," +
             "\"id\":\"id\"," +
             "\"fields\":[" +
               "{\"name\":\"ProductType\",\"mapping\":\"ProductType\"}," +
               "{\"name\":\"ProductName\",\"mapping\":\"ProductName\"}," +
               "{\"name\":\"ReleaseType\",\"mapping\":\"ReleaseType\"}," +
               "{\"name\":\"StartTime\",\"mapping\":\"StartTime\"}," +
               "{\"name\":\"EndTime\",\"mapping\":\"EndTime\"}," +
               "{\"name\":\"State\",\"mapping\":\"State\"}," +
               "{\"name\":\"Address\",\"mapping\":\"Address\"}," +
               "{\"name\":\"User\",\"mapping\":\"User\"}," +
               "{\"name\":\"IPAddress\",\"mapping\":\"IPAddress\"}," +
               "{\"name\":\"Detail\",\"mapping\":\"Detail\"}," +
               "{\"name\":\"DeadLine\",\"mapping\":\"DeadLine\"}," +
               "{\"name\":\"Type\",\"mapping\":\"Type\"}," +
               "{\"name\":\"FileTempPath\",\"mapping\":\"FileTempPath\"}" +
             "]" +
           "}," +
           "\"results\":\"" + totalList.Count + "\",\"" +
           "rows\":[" +
            showData.ToString().TrimEnd(',') +
           "]" +
         "}";
            }
            return strJson;
        }

        public DataTable GetPublishLogDataPast()
        {
            string strSQL = "SELECT * FROM T_ProductLog";
            DataTable dt = m_Database.GetDataTable(strSQL);
            return dt;
        }

        //2015年12月25日，根据发布日期获取AQI分时段的预报内容
        public string GetAQIPeriodLogDetail(string releaseDate)
        {
            //预报的日期
            //string strConvertDate = ConvertDatetime("2015年12月25日");
            //string strSQL = "SELECT ITEMID,AQI FROM T_ForecastGroup WHERE ForecastDate='"+strConvertDate+"' AND durationID in (6,2,3) AND ITEMID='"+"6";
            //DataTable dt = m_Database.GetDataTable(strSQL);
            //StringBuilder sb = new StringBuilder();
            //if (dt.Rows.Count > 0)
            //{
            //    //标记是哪一天（今天，明天或者后天）
            //    string strJsonPrefix = "";
            //    for (int i = 0; i < dt.Rows.Count; i++)
            //    {
            //        if (i == 0)
            //        {
            //            strJsonPrefix = "";
            //        }
            //        else if (i == 1)
            //        {
            //            strJsonPrefix = "";
            //        }
            //        if (i == 0)
            //        {
            //            strJsonPrefix = "";
            //        }
            //        sb.Append("{\"" + "itemID" + "\":\"" + dt.Rows[i]["ITEMID"] + "\",\"" + "aqi" + "\":\"" + dt.Rows[i]["AQI"]+"\"");
            //    }

            //}
            //return "细节内容";

            return "{\"todayAQI\":\"56\",\"todayItemID\":\"1\"}";
        }

        //将文本内容发布到ftp
        public string UpLoadTxtFtpLatestCopy(string ftpString, string fileDate, string functionName, string txtContent)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            if (txtContent == "" || txtContent == null)
            {
                return "文本内容不能为空！";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                else if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                else if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                else if (strFileName.Contains("SHMM"))
                                {
                                    strDate = (date.Month > 9) ? date.Month.ToString() : ("0" + date.Month.ToString());
                                    strFileName = strFileName.Replace("MM", strDate);
                                }
                                else if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }

                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    try
                                    {
                                        string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        string strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        //IPHostEntry ipe = Dns.GetHostEntry(Dns.GetHostName());
                                        //IPAddress ip = ipe.AddressList[0];
                                        //string clientIPAddress = System.Net.Dns.GetHostAddresses(Dns.GetHostName()).GetValue(0).ToString();
                                        string strIP = HttpClientHelper.GetIP();
                                        InsertFTPUpLoadLog("AQIProduct", functionName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString, "user", strIP, strEnd, "Type");

                                        //strInsertSQL = "INSERT INTO T_ProductLog (ProductType, ProductName, ReleaseType,StartTime,EndTime,State,Address,[User],IPAddress,Detail,DeadLine,Type) ";
                                        //string strAQL = String.Format("SELECT '{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}'", "AQIPeriod", "AQIPeriod", "FTP", strStart, strEnd, strPubState, strFTPIPString, "User", "IPAddress", "Detail", strEnd, "Type");
                                        //strInsertSQL += strAQL;
                                        //m_Database.Execute(strInsertSQL);
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;
                }
            }
            return "成功" + intSuccessCount.ToString() + "个，失败" + intFailCount.ToString() + "个";
        }

        public string UpLoadTxtFtpLatest(string ftpString, string fileDate, string functionName, string txtContent, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            if (txtContent == "" || txtContent == null)
            {
                return "文本内容不能为空！";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];

                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                if (strFileName.Contains("MMDD"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("MMDD", strDate);
                                }

                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    try
                                    {
                                        string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        string strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        string strIP = HttpClientHelper.GetIP();
                                        string strUseFuncName = (functionName == "OzoneForecast") ? "臭氧预报" : functionName;
                                        InsertFTPUpLoadLog("AQI预报产品", strUseFuncName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;

                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                    if (functionName == "UVForecast")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 15:20:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 18:20:00.000");
                        functionName = functionName + "_17";
                    }
                    else if (functionName == "OzoneForecast")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:40:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 19:40:00.000");
                    }
                    if (intSuccessCount == ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //表示发布不完全
                        return "less";
                    }


                    //if (intSuccessCount == ftpList.Length)
                    //{
                    //    //表示全部发布成功
                    //    return "success";
                    //}
                    //else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    //{
                    //    //表示发布不完全
                    //    return "less";
                    //}
                }
            }
            return "fail";
        }

        public string UpLoadTxtFtpLatestForOzone(string ftpString, string fileDate, string functionName, string txtContent, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            if (txtContent == "" || txtContent == null)
            {
                return "文本内容不能为空！";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            //ftp文本保存临时路径，用语发布日志预览
            string strFtpTempSavePath = ConfigurationManager.AppSettings["FtpUploadTxtTempPath"].ToString();
            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {
                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];

                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                if (strFileName.Contains("MMDD"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("MMDD", strDate);
                                }

                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    string strStart = "";
                                    string strPubState = "";
                                    string strEnd = "";
                                    string strIP = "";
                                    try
                                    {
                                        strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        strIP = HttpClientHelper.GetIP();
                                        //string strUseFuncName = (functionName == "OzoneForecast") ? "臭氧预报" : functionName;
                                        //InsertFTPUpLoadLog("AQI预报产品", strUseFuncName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //将发布的文本在服务器固定路径存储，用于发布日志预览
                                    if (!Directory.Exists(strFtpTempSavePath))
                                    {
                                        Directory.CreateDirectory(strFtpTempSavePath);
                                    }
                                    StringBuilder sb = new StringBuilder(txtContent);
                                    string strTempContent = sb.ToString().Replace("\n", "\r\n");
                                    using (FileStream tempFs = new FileStream(strFtpTempSavePath + strFileName, FileMode.OpenOrCreate))
                                    {
                                        StreamWriter sw = new StreamWriter(tempFs);
                                        sw.Write(strTempContent);
                                        sw.Close();
                                    }
                                    string strTxtProName = "";
                                    switch (ftpList[i].Split(',')[0])
                                    {
                                        case "InfoCenterFtp":
                                            strTxtProName = "上海臭氧预报产品(上传至信息中心)";
                                            break;
                                        case "62WebSite":
                                            strTxtProName = "上海臭氧预报产品(上传至62网站)";
                                            break;
                                        default:
                                            strTxtProName = "上海臭氧预报产品";
                                            break;
                                    }
                                    InsertFTPUpLoadLogNew("臭氧预报产品", strTxtProName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strFtpTempSavePath + strFileName);
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;

                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                    if (functionName == "UVForecast")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 15:20:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 18:20:00.000");
                        functionName = functionName + "_17";
                    }
                    else if (functionName == "OzoneForecast")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:40:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 19:40:00.000");
                    }
                    if (intSuccessCount == ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //表示发布不完全
                        return "less";
                    }


                    //if (intSuccessCount == ftpList.Length)
                    //{
                    //    //表示全部发布成功
                    //    return "success";
                    //}
                    //else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    //{
                    //    //表示发布不完全
                    //    return "less";
                    //}
                }
            }
            return "fail";
        }


        public string UpLoadTxtFtpLatestForAQIPeriodPast(string ftpString, string fileDate, string functionName, string txtContent, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            if (txtContent == "" || txtContent == null)
            {
                return "文本内容不能为空！";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            //ftp文本保存临时路径，用语发布日志预览
            string strFtpTempSavePath = ConfigurationManager.AppSettings["FtpUploadTxtTempPath"].ToString();
            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }
                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];

                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                if (strFileName.Contains("MMDD"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("MMDD", strDate);
                                }

                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    string strStart = "";
                                    string strEnd = "";
                                    string strPubState = "1";
                                    string strIP = "";
                                    try
                                    {
                                        strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        strIP = HttpClientHelper.GetIP();
                                        //InsertFTPUpLoadLog("AQI分时段产品", "AQI分时段预报文本", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //将发布的文本在服务器固定路径存储，用于发布日志预览
                                    if (!Directory.Exists(strFtpTempSavePath))
                                    {
                                        Directory.CreateDirectory(strFtpTempSavePath);
                                    }
                                    StringBuilder sb = new StringBuilder(txtContent);
                                    string strTempContent = sb.ToString().Replace("\n", "\r\n");
                                    using (FileStream tempFs = new FileStream(strFtpTempSavePath + strFileName, FileMode.OpenOrCreate))
                                    {
                                        StreamWriter sw = new StreamWriter(tempFs);
                                        sw.Write(strTempContent);
                                        sw.Close();
                                    }
                                    string strProName = "";
                                    switch (ftpList[i].Split(',')[0])
                                    {
                                        case "InfoCenterFtp":
                                            strProName = "AQI分时段预报(上传信息中心)";
                                            break;
                                        case "SciServiceCenter":
                                            strProName = "AQI分时段预报(上传科技服务中心)";
                                            break;
                                        case "AQILocal":
                                            strProName = "分时段AQI预报上传到fserver";
                                            break;
                                        case "AQILocal62":
                                            strProName = "分时段AQI152业务气象台传文件";
                                            break;
                                        case "62WebSite":
                                            strProName = "AQI分时段预报(上传62网站)";
                                            break;
                                        default:
                                             strProName = "分时段文件";
                                            break;
                                    }
                                    InsertFTPUpLoadLogNew("AQI预报", strProName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strFtpTempSavePath + strFileName);
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;
                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");
                    if (intSuccessCount == ftpList.Length)
                    {
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        //表示发布不完全
                        return "less";
                    }
                }
            }
            return "fail";
        }

        public string UpLoadTxtFtpLatestForAQIPeriodCopy(string ftpString, string fileDate, string functionName, string txtContent, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            if (txtContent == "" || txtContent == null)
            {
                return "文本内容不能为空！";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            //ftp文本保存临时路径，用语发布日志预览
            string strFtpTempSavePath = ConfigurationManager.AppSettings["FtpUploadTxtTempPath"].ToString();
            string strTxtTempPath = "";
            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }
                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];

                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                if (strFileName.Contains("MMDD"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("MMDD", strDate);
                                }

                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    string strStart = "";
                                    string strEnd = "";
                                    string strPubState = "1";
                                    string strIP = "";
                                    try
                                    {
                                        strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTPAQIPeriod(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        strIP = HttpClientHelper.GetIP();
                                        //InsertFTPUpLoadLog("AQI分时段产品", "AQI分时段预报文本", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //将发布的文本在服务器固定路径存储，用于发布日志预览
                                    if (!Directory.Exists(strFtpTempSavePath))
                                    {
                                        Directory.CreateDirectory(strFtpTempSavePath);
                                    }
                                    StringBuilder sb = new StringBuilder(txtContent);
                                    string strTempContent = sb.ToString().Replace("\n", "\r\n");
                                    using (FileStream tempFs = new FileStream(strFtpTempSavePath +"AQIPeriod\\"+DateTime.Now.ToString("yyyyMMddHHmmss")+"_Text_"+ strFileName, FileMode.OpenOrCreate))
                                    {
                                        StreamWriter sw = new StreamWriter(tempFs);
                                        sw.Write(strTempContent);
                                        sw.Close();
                                    }
                                    string strProName = "";
                                    switch (ftpList[i].Split(',')[0])
                                    {
                                        case "InfoCenterFtp":
                                            strProName = "AQI分时段预报(上传信息中心)";
                                            break;
                                        case "SciServiceCenter":
                                            strProName = "AQI分时段预报(上传科技服务中心)";
                                            break;
                                        case "AQILocal":
                                            strProName = "分时段AQI预报上传到fserver";
                                            break;
                                        case "AQILocal62":
                                            strProName = "分时段AQI152业务气象台传文件";
                                            break;
                                        case "62WebSite":
                                            strProName = "AQI分时段预报(上传62网站)";
                                            break;
                                        default:
                                            strProName = "分时段文件";
                                            break;
                                    }
                                    strTxtTempPath = strFtpTempSavePath + "AQIPeriod\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_Text_" + strFileName;
                                    InsertFTPUpLoadLogNew("AQI预报", strProName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strTxtTempPath);
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;
                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");
                    if (intSuccessCount == ftpList.Length)
                    {
                        //表示全部发布成功
                        return "success" + "&" + strTxtTempPath;
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        //表示发布不完全
                        return "less";
                    }
                }
            }
            return "fail";
        }

        public string UpLoadTxtFtpLatestForAQIPeriod(string ftpString, string fileDate, string functionName, string txtContent, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            if (txtContent == "" || txtContent == null)
            {
                return "文本内容不能为空！";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            //ftp文本保存临时路径，用语发布日志预览
            string strFtpTempSavePath = ConfigurationManager.AppSettings["FtpUploadTxtTempPath"].ToString();
            string strTxtTempPath = "";
            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }
                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];

                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                if (strFileName.Contains("MMDD"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("MMDD", strDate);
                                }

                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    string strStart = "";
                                    string strEnd = "";
                                    string strPubState = "1";
                                    string strIP = "";
                                    try
                                    {
                                        strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTPAQIPeriod(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        strIP = HttpClientHelper.GetIP();
                                        //InsertFTPUpLoadLog("AQI分时段产品", "AQI分时段预报文本", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //将发布的文本在服务器固定路径存储，用于发布日志预览
                                    if (!Directory.Exists(strFtpTempSavePath))
                                    {
                                        Directory.CreateDirectory(strFtpTempSavePath);
                                    }
                                    StringBuilder sb = new StringBuilder(txtContent);
                                    string strTempContent = sb.ToString().Replace("\n", "\r\n");
                                    using (FileStream tempFs = new FileStream(strFtpTempSavePath + "AQIPeriod\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_Text_" + strFileName, FileMode.OpenOrCreate))
                                    {
                                        StreamWriter sw = new StreamWriter(tempFs);
                                        sw.Write(strTempContent);
                                        sw.Close();
                                    }
                                    string strProName = "";
                                    switch (ftpList[i].Split(',')[0])
                                    {
                                        case "InfoCenterFtp":
                                            strProName = "AQI分时段预报(上传信息中心)";
                                            break;
                                        case "SciServiceCenter":
                                            strProName = "AQI分时段预报(上传科技服务中心)";
                                            break;
                                        case "AQILocal":
                                            strProName = "分时段AQI预报上传到fserver";
                                            break;
                                        case "AQILocal62":
                                            strProName = "分时段AQI152业务气象台传文件";
                                            break;
                                        case "62WebSite":
                                            strProName = "AQI分时段预报(上传62网站)";
                                            break;
                                        default:
                                            strProName = "分时段文件";
                                            break;
                                    }
                                    strTxtTempPath = strFtpTempSavePath + "AQIPeriod\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_Text_" + strFileName;
                                    InsertFTPUpLoadLogNew("AQI预报", strProName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strTxtTempPath);
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;
                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");
                    if (intSuccessCount == ftpList.Length)
                    {
                        //表示全部发布成功
                        return "success" + "&" + strTxtTempPath;
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        //表示发布不完全
                        return "less";
                    }
                }
            }
            return "fail";
        }

        //霾预报的文本发布
        public string UpLoadTxtFtpLatestForHaze(string ftpString, string fileDate, string functionName, string txtContent, string userName, string hourType)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            if (txtContent == "" || txtContent == null)
            {
                return "文本内容不能为空！";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            //ftp文本保存临时路径，用语发布日志预览
            string strFtpTempSavePath = ConfigurationManager.AppSettings["FtpUploadTxtTempPath"].ToString();
            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];

                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDDHH"))
                                {
                                    if (hourType == "05")
                                    {
                                        strDate = date.ToString("yyMMdd05");
                                    }
                                    else
                                    {
                                        strDate = date.ToString("yyMMdd17");
                                    }
                                    strFileName = strFileName.Replace("YYMMDDHH", strDate);
                                }
                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                if (strFileName.Contains("MMDD"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("MMDD", strDate);
                                }

                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    string strStart = "";
                                    string strPubState = "";
                                    string strEnd = "";
                                    string strIP = "";
                                    try
                                    {
                                        strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        strIP = HttpClientHelper.GetIP();
                                        //InsertFTPUpLoadLog("AQI预报产品", "霾预报文本", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //将发布的文本在服务器固定路径存储，用于发布日志预览
                                    if (!Directory.Exists(strFtpTempSavePath))
                                    {
                                        Directory.CreateDirectory(strFtpTempSavePath);
                                    }
                                    StringBuilder sb = new StringBuilder(txtContent);
                                    string strTempContent = sb.ToString().Replace("\n", "\r\n");
                                    using (FileStream tempFs = new FileStream(strFtpTempSavePath + strFileName, FileMode.OpenOrCreate))
                                    {
                                        StreamWriter sw = new StreamWriter(tempFs);
                                        sw.Write(strTempContent);
                                        sw.Close();
                                    }
                                    InsertFTPUpLoadLogNew("霾预报", "霾预报", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strFtpTempSavePath + strFileName);
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;

                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                    if (hourType == "05")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 05:00:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 08:00:00.000");
                        functionName = functionName + "_05";
                    }
                    else if (hourType == "17")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                        functionName = functionName + "_17";
                    }
                    //if (functionName == "UVForecast")
                    //{
                    //    strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 09:45:00.000");
                    //    strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 12:45:00.000");
                    //}
                    //else if (functionName == "OzoneForecast")
                    //{
                    //    strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:40:00.000");
                    //    strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 19:40:00.000");
                    //}
                    if (intSuccessCount == ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //表示发布不完全
                        return "less";
                    }


                    //if (intSuccessCount == ftpList.Length)
                    //{
                    //    //表示全部发布成功
                    //    return "success";
                    //}
                    //else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    //{
                    //    //表示发布不完全
                    //    return "less";
                    //}
                }
            }
            return "fail";
        }

        //上传既有文本又有图片
        public string UpLoadTxtAndImgFtpLatestCopy(string ftpString, string fileDate, string sourceFileName, string functionName, string txtContent, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            //if (txtContent == "" || txtContent == null)
            //{
            //    return "fail";
            //}
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }

                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    try
                                    {
                                        string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        string strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        //在状态表T_State当中插入记录
                                        string strIP = HttpClientHelper.GetIP();

                                        InsertFTPUpLoadLog("AQIProduct", functionName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }

                                }
                                else if (strFileSuffix == "GIF" || strFileSuffix == "gif" || strFileSuffix == "jpg")
                                {
                                    string strIP = HttpClientHelper.GetIP();
                                    string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    string strPubState = "1";
                                    string strFtpResult = "fail";
                                    try
                                    {
                                        strFtpResult = UpLoadImg(strFTPIPString, strFTPUser, strFTPPSW, sourceFileName, strFileName);
                                        strPubState = strFtpResult == "success" ? "0" : "1";
                                        //string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        ////在状态表T_State当中插入记录                                       
                                        //InsertFTPUpLoadLog("AQIProduct", functionName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    finally
                                    {
                                        string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        //在状态表T_State当中插入记录                                       
                                        InsertFTPUpLoadLog("AQIProduct", functionName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                    }
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;
                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");

                    if (intSuccessCount == ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //表示发布不完全
                        return "less";
                    }
                }
            }
            return "fail";
        }

        public string UpLoadTxtAndImgFtpLatestCopy(string ftpString, string fileDate, string sourceFileName, string functionName, string txtContent, string userName, string hourType)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            //if (txtContent == "" || txtContent == null)
            //{
            //    return "fail";
            //}
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            //ftp文本保存临时路径，用语发布日志预览
            string strFtpTempSavePath = ConfigurationManager.AppSettings["FtpUploadTxtTempPath"].ToString();
            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                if (strFileName.Contains("scuem_WRTJ_YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMdd2000");
                                    strFileName = strFileName.Replace("scuem_WRTJ_YYYYMMDDHHmm", "scuem_WRTJ_" + strDate);
                                }
                                if (strFileName.Contains("WRTJ_YYYYMMDDHH"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    if (hourType == "07")
                                    {
                                        strFileName = strFileName.Replace("WRTJ_YYYYMMDDHH", "WRTJ_" + strDate + "08");
                                    }
                                    else if (hourType == "17")
                                    {
                                        strFileName = strFileName.Replace("WRTJ_YYYYMMDDHH", "WRTJ_" + strDate + "20");
                                    }
                                }
                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }

                                //起报时间
                                if (strFileName.Contains("YYYYMMDDHH"))
                                {
                                    if (hourType == "07")
                                    {
                                        strDate = date.AddDays(-2).ToString("yyyyMMdd20");
                                    }
                                    else if (hourType == "17")
                                    {
                                        strDate = date.AddDays(-1).ToString("yyyyMMdd20");
                                    }
                                    //strDate = date.AddDays(-1).ToString("yyyyMMdd20");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);

                                }
                                //发布时间
                                if (strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMdd08");
                                    if (hourType == "07")
                                    {
                                        strDate = date.ToString("yyyyMMdd08");
                                    }
                                    else if (hourType == "17")
                                    {
                                        strDate = date.ToString("yyyyMMdd20");
                                    }
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                //预报时间
                                if (strFileName.Contains("mmdd"))
                                {
                                    if (hourType == "07")
                                    {
                                        strDate = date.ToString("MMdd");
                                    }
                                    else if (hourType == "17")
                                    {
                                        strDate = date.AddDays(1).ToString("MMdd");
                                    }
                                    //strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    string strStart = "";
                                    string strEnd = "";
                                    string strPubState = "";
                                    string strIP = "";
                                    try
                                    {
                                        strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        strIP = HttpClientHelper.GetIP();

                                        //InsertFTPUpLoadLog("空气污染气象条件", "空气污染气象条件预报文本", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //将发布的文本在服务器固定路径存储，用于发布日志预览
                                    if (!Directory.Exists(strFtpTempSavePath))
                                    {
                                        Directory.CreateDirectory(strFtpTempSavePath);
                                    }
                                    StringBuilder sb = new StringBuilder(txtContent);
                                    string strTempContent = sb.ToString().Replace("\n", "\r\n");
                                    using (FileStream tempFs = new FileStream(strFtpTempSavePath + strFileName, FileMode.OpenOrCreate))
                                    {
                                        StreamWriter sw = new StreamWriter(tempFs);
                                        sw.Write(strTempContent);
                                        sw.Close();
                                    }
                                    InsertFTPUpLoadLogNew("AQI分区预报", "AQI分区预报文本", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type", strFtpTempSavePath + strFileName);
                                }
                                else if (strFileSuffix == "GIF" || strFileSuffix == "gif" || strFileSuffix == "jpg")
                                {
                                    string strIP = HttpClientHelper.GetIP();
                                    string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    string strPubState = "1";
                                    string strFtpResult = "fail";
                                    try
                                    {
                                        strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        strFtpResult = UpLoadImgExportMapCopy(strFTPIPString, strFTPUser, strFTPPSW, sourceFileName, strFileName);
                                        string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        InsertFTPUpLoadLog("空气污染气象条件", "空气污染气象条件图片", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        strPubState = strFtpResult == "success" ? "0" : "1";
                                        if (strPubState == "0")
                                        {
                                            intSuccessCount++;
                                        }
                                        else
                                        {
                                            intFailCount++;
                                        }

                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    finally
                                    {
                                        string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        //在状态表T_State当中插入记录                                       
                                        InsertFTPUpLoadLog("AQIProduct", functionName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                    }
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;
                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");


                    if (hourType == "07")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 08:00:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 11:00:00.000");
                        functionName = functionName + "_05";
                    }
                    else if (hourType == "17")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 23:00:00.000");
                        functionName = functionName + "_17";
                    }

                    if (intSuccessCount == ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //表示发布不完全
                        return "less";
                    }
                }
            }
            return "fail";
        }

        //sourceFileName为上传图片的路径
        public string UpLoadTxtAndImgFtpLatest(string ftpString, string fileDate, string functionName, string txtContent, string userName, string hourType)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            //if (txtContent == "" || txtContent == null)
            //{
            //    return "fail";
            //}
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            //ftp文本保存临时路径，用语发布日志预览
            string strFtpTempSavePath = ConfigurationManager.AppSettings["FtpUploadTxtTempPath"].ToString();
            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {
                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                if (strFileName.Contains("scuem_WRTJ_YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMdd2000");
                                    strFileName = strFileName.Replace("scuem_WRTJ_YYYYMMDDHHmm", "scuem_WRTJ_" + strDate);
                                }
                                if (strFileName.Contains("WRTJ_YYYYMMDDHH"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    if (hourType == "07")
                                    {
                                        strFileName = strFileName.Replace("WRTJ_YYYYMMDDHH", "WRTJ_" + strDate + "08");
                                    }
                                    else if (hourType == "17")
                                    {
                                        strFileName = strFileName.Replace("WRTJ_YYYYMMDDHH", "WRTJ_" + strDate + "20");
                                    }
                                }
                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }

                                //起报时间
                                if (strFileName.Contains("YYYYMMDDHH"))
                                {
                                    if (hourType == "07")
                                    {
                                        strDate = date.AddDays(-2).ToString("yyyyMMdd20");
                                    }
                                    else if (hourType == "17")
                                    {
                                        strDate = date.AddDays(-1).ToString("yyyyMMdd20");
                                    }
                                    //strDate = date.AddDays(-1).ToString("yyyyMMdd20");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);

                                }
                                //发布时间
                                if (strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMdd08");
                                    if (hourType == "07")
                                    {
                                        strDate = date.ToString("yyyyMMdd08");
                                    }
                                    else if (hourType == "17")
                                    {
                                        strDate = date.ToString("yyyyMMdd20");
                                    }
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                //预报时间
                                if (strFileName.Contains("mmdd"))
                                {
                                    if (hourType == "07")
                                    {
                                        strDate = date.ToString("MMdd");
                                    }
                                    else if (hourType == "17")
                                    {
                                        strDate = date.AddDays(1).ToString("MMdd");
                                    }
                                    //strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    string strStart = "";
                                    string strEnd = "";
                                    string strPubState = "";
                                    string strIP = "";
                                    try
                                    {
                                        strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        strIP = HttpClientHelper.GetIP();

                                        //InsertFTPUpLoadLog("空气污染气象条件", "空气污染气象条件预报文本", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //将发布的文本在服务器固定路径存储，用于发布日志预览
                                    if (!Directory.Exists(strFtpTempSavePath))
                                    {
                                        Directory.CreateDirectory(strFtpTempSavePath);
                                    }
                                    StringBuilder sb = new StringBuilder(txtContent);
                                    string strTempContent = sb.ToString().Replace("\n", "\r\n");
                                    using (FileStream tempFs = new FileStream(strFtpTempSavePath + strFileName, FileMode.OpenOrCreate))
                                    {
                                        StreamWriter sw = new StreamWriter(tempFs);
                                        sw.Write(strTempContent);
                                        sw.Close();
                                    }
                                    string strTxtProName = "";
                                    switch (ftpList[i].Split(',')[0])
                                    {
                                        case "zxt":
                                            strTxtProName = "空气污染气象条件分区预报稿";
                                            break;
                                        case "InfoCenterFtp":
                                            strTxtProName = "空气污染气象条件分区预报稿";
                                            break;
                                        case "62WebSite":
                                            strTxtProName = "分区空气污染气象条件scuem";
                                            break;
                                        default:
                                            strTxtProName = "空气污染气象条件分区预报稿";
                                            break;
                                    }
                                    InsertFTPUpLoadLogNew("空气污染气象条件", strTxtProName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strFtpTempSavePath + strFileName);
                                }
                                else if (strFileSuffix == "GIF" || strFileSuffix == "gif" || strFileSuffix == "jpg")
                                {
                                    string strIP = HttpClientHelper.GetIP();
                                    string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    string strPubState = "1";
                                    string strFtpResult = "fail";
                                    //图片存储的临时路径，用语发布日志查看
                                    string strTempImgPath = "";
                                    try
                                    {
                                        strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        strFtpResult = UpLoadImgExportMap(strFTPIPString, strFTPUser, strFTPPSW, hourType);
                                        string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        //InsertFTPUpLoadLog("空气污染气象条件", "空气污染气象条件图片", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type");
                                        //strPubState = strFtpResult.Split('&')[0] == "success" ? "0" : "1";
                                        if (strFtpResult.Contains("&"))
                                        {
                                            if (strFtpResult.Split('&')[0] == "success")
                                            {
                                                strPubState = "0";
                                            }
                                            //将图片保存在服务器临时路径，发布日志预览显示
                                            strTempImgPath = SaveImgToTempPath(strFtpResult.Split('&')[1], strFtpResult.Split('&')[2]);
                                        }
                                        else
                                        {
                                            strPubState = "1";
                                        }
                                        if (strPubState == "0")
                                        {
                                            intSuccessCount++;
                                        }
                                        else
                                        {
                                            intFailCount++;
                                        }

                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    finally
                                    {
                                        string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        //在状态表T_State当中插入记录                                       
                                        //InsertFTPUpLoadLog("空气污染气象条件", "空气污染气象条件图片", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type");
                                        string strGifProName = "";
                                        switch (ftpList[i].Split(',')[0])
                                        {
                                            case "InfoCenterFtp":
                                                strGifProName = "空气污染气象条件落区预报图";
                                                break;
                                            case "port21":
                                                strGifProName = "空气污染气象条件落区预报图";
                                                break;
                                            case "AirPollutionForecast2":
                                                strGifProName = "空气污染气象条件落区预报图";
                                                break;
                                            default:
                                                strGifProName = "";
                                                break;
                                        }

                                        InsertFTPUpLoadLogNew("空气污染气象条件", strGifProName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strTempImgPath);
                                    }
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;
                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");

                    if (hourType == "07")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 07:00:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 10:00:00.000");
                        functionName = functionName + "_07";
                    }
                    else if (hourType == "17")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                        functionName = functionName + "_17";
                    }

                    if (intSuccessCount == ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //表示发布不完全
                        return "less";
                    }
                }
            }
            return "fail";
        }

        public string AQIjisuan(int AQI)
        {
            switch (AQI.ToString().Length)
            {
                case 1:
                    return "000" + AQI.ToString();
                case 2:
                    return "00" + AQI.ToString();
                case 3:
                    return "0" + AQI.ToString();
                default:
                    return "9999";
            }
        }

        public string Valuejisuan(double denValue)
        {
            string strValuReturn;
            strValuReturn = (denValue * 100).ToString();
            switch (strValuReturn.Length)
            {
                case 0:
                    strValuReturn = "000000" + strValuReturn;
                    break;
                case 1:
                    strValuReturn = "00000" + strValuReturn;
                    break;
                case 2:
                    strValuReturn = "0000" + strValuReturn;
                    break;
                case 3:
                    strValuReturn = "000" + strValuReturn;
                    break;
                case 4:
                    strValuReturn = "00" + strValuReturn;
                    break;
                case 5:
                    strValuReturn = "0" + strValuReturn;
                    break;
                case 6:
                    strValuReturn = "" + strValuReturn;
                    break;

            }
            return strValuReturn;
        }

        public string FirstItemCopy(string firstItem)
        {
            string strValuReturn = null;
            int intITEMID = Convert.ToInt32(firstItem);

            strValuReturn = firstItem + "99999";
            return strValuReturn;
        }

        public string FirstItemChange(string firstItem)
        {
            string strValuReturn = null;
            int intITEMID = Convert.ToInt32(firstItem);

            strValuReturn = firstItem + "99999";
            return strValuReturn;
        }

        public string FirstItem(string firstItem)
        {
            string strValuReturn = null;
            int intITEMID = Convert.ToInt32(firstItem);
            string strUseItem = "";
            switch (firstItem)
            {
                case "1":
                    strUseItem = "6";
                    break;
                case "2":
                    strUseItem = "3";
                    break;
                case "3":
                    strUseItem = "2";
                    break;
                case "4":
                    strUseItem = "5";
                    break;
                case "6":
                    strUseItem = "4";
                    break;
                case "7":
                    strUseItem = "1";
                    break;
            }
            strValuReturn = strUseItem + "99999";
            return strValuReturn;
        }

        //2015年12月28日
        public string GetReportText(DataTable dt, string sitrID, string AQIValue, string aqiItem, string xValue, string yValue)
        {
            StringBuilder sbtxtdata = new StringBuilder();
            StringBuilder sb = new StringBuilder();
            string Lines = "";
            if (dt.Rows.Count > 0)
            {
                string time = dt.Rows[0][0].ToString();

                int intInterval = 0;
                //根据总时间和间隔计算行数
                int intLineCount = 16;
                List<StringBuilder> allLines = new List<StringBuilder>();
                StringBuilder singleLine = null;
                string strLineHead = "00";
                for (int i = 0; i < intLineCount; i++)
                {
                    //世纪使用的一行开头的数字                    
                    switch (i)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                        case 5:
                        case 6:
                        case 7:
                            strLineHead = ((i + 1) * 3) > 9 ? ((i + 1) * 3).ToString() : "0" + ((i + 1) * 3).ToString();
                            break;
                        case 8:
                            strLineHead = "30";
                            break;
                        case 9:
                            strLineHead = "36";
                            break;
                        case 10:
                            strLineHead = "42";
                            break;
                        case 11:
                            strLineHead = "48";
                            break;
                        case 12:
                            strLineHead = "54";
                            break;
                        case 13:
                            strLineHead = "60";
                            break;
                        case 14:
                            strLineHead = "66";
                            break;
                        case 15:
                            strLineHead = "72";
                            break;
                    }
                    singleLine = new StringBuilder();
                    singleLine.Append(strLineHead + " ");
                    allLines.Add(singleLine);
                }
                int intIntervalValue = 0;
                double intDenValue = 0;
                //标记每一行内添加文本的次数，与AQIItem对应
                int intCycleIndex = 0;
                for (int rows = 0; rows < dt.Rows.Count; rows++, intCycleIndex = 0)
                {
                    try
                    {
                        intCycleIndex++;

                        intIntervalValue = Convert.ToInt32(dt.Rows[rows]["Interval"]);
                        switch (intIntervalValue)
                        {
                            case 54:
                                intInterval = 8;
                                break;
                            case 60:
                                intInterval = 9;
                                break;
                            case 66:
                                intInterval = 10;
                                break;
                            case 72:
                                intInterval = 11;
                                break;
                            case 78:
                                intInterval = 12;
                                break;
                            case 84:
                                intInterval = 13;
                                break;
                            case 90:
                                intInterval = 14;
                                break;
                            case 96:
                                intInterval = 15;
                                break;
                            default: intInterval = (intIntervalValue - 24) / 3 - 1;
                                break;

                        }
                        //intInterval = (intIntervalValue - 24) / 3 - 1;
                        intDenValue = Convert.ToDouble(dt.Rows[rows]["Value"]);
                        allLines[intInterval].Append(Valuejisuan(intDenValue) + " ");
                    }
                    catch (Exception e)
                    {

                    }
                }

                sb.Append(Lines);
                sb.Append("\r\n" + sitrID + " " + yValue + " " + xValue + " 00189 16 09\r\n");
                for (int j = 0; j < allLines.Count; j++)
                {
                    //暂时没有CO数据
                    //allLines[j].Append("999999");
                    if (j != 7)
                    {
                        allLines[j].Append("9999");
                        allLines[j].Append(" 9");
                        allLines[j].Append(" 999999");
                    }
                    //24小时AQI预报值与等级
                    else
                    {
                        allLines[j].Append(AQIjisuan(Convert.ToInt32(AQIValue)));
                        allLines[j].Append(" " + CalculateAQLLevelNo(AQIValue));
                        if (CalculateAQLLevelNo(AQIValue) == 1)
                        {
                            allLines[j].Append(" " + "099999");
                        }
                        else
                        {
                            allLines[j].Append(" " + FirstItem(aqiItem));
                        }
                    }
                    if (j < allLines.Count - 1)
                    {
                        sb.Append(allLines[j].ToString() + "\r\n");
                    }
                    else
                    {
                        sb.Append(allLines[j].ToString());
                    }
                }
            }
            return sb.ToString();
        }

        public string GetReportTextChange(DataTable dt, string sitrID, string AQIValue, string aqiItem, string xValue, string yValue)
        {
            StringBuilder sbtxtdata = new StringBuilder();
            StringBuilder sb = new StringBuilder();
            string Lines = "";
            if (dt.Rows.Count > 0)
            {
                string time = dt.Rows[0][0].ToString();

                int intInterval = 0;
                //根据总时间和间隔计算行数
                int intLineCount = 16;
                List<StringBuilder> allLines = new List<StringBuilder>();
                StringBuilder singleLine = null;
                string strLineHead = "00";
                for (int i = 0; i < intLineCount; i++)
                {
                    //世纪使用的一行开头的数字                    
                    switch (i)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                        case 5:
                        case 6:
                        case 7:
                            strLineHead = ((i + 1) * 3) > 9 ? ((i + 1) * 3).ToString() : "0" + ((i + 1) * 3).ToString();
                            break;
                        case 8:
                            strLineHead = "30";
                            break;
                        case 9:
                            strLineHead = "36";
                            break;
                        case 10:
                            strLineHead = "42";
                            break;
                        case 11:
                            strLineHead = "48";
                            break;
                        case 12:
                            strLineHead = "54";
                            break;
                        case 13:
                            strLineHead = "60";
                            break;
                        case 14:
                            strLineHead = "66";
                            break;
                        case 15:
                            strLineHead = "72";
                            break;
                    }
                    singleLine = new StringBuilder();
                    singleLine.Append(strLineHead + " ");
                    allLines.Add(singleLine);
                }
                int intIntervalValue = 0;
                double intDenValue = 0;
                //标记每一行内添加文本的次数，与AQIItem对应
                int intCycleIndex = 0;
                for (int rows = 0; rows < dt.Rows.Count; rows++, intCycleIndex = 0)
                {
                    try
                    {
                        intCycleIndex++;

                        intIntervalValue = Convert.ToInt32(dt.Rows[rows]["Interval"]);
                        switch (intIntervalValue)
                        {
                            case 54:
                                intInterval = 8;
                                break;
                            case 60:
                                intInterval = 9;
                                break;
                            case 66:
                                intInterval = 10;
                                break;
                            case 72:
                                intInterval = 11;
                                break;
                            case 78:
                                intInterval = 12;
                                break;
                            case 84:
                                intInterval = 13;
                                break;
                            case 90:
                                intInterval = 14;
                                break;
                            case 96:
                                intInterval = 15;
                                break;
                            default: intInterval = (intIntervalValue - 24) / 3 - 1;
                                break;

                        }
                        //intInterval = (intIntervalValue - 24) / 3 - 1;
                        intDenValue = Convert.ToDouble(dt.Rows[rows]["Value"]);
                        allLines[intInterval].Append(Valuejisuan(intDenValue) + " ");
                    }
                    catch (Exception e)
                    {

                    }
                }

                sb.Append(Lines);
                sb.Append("\r\n" + sitrID + " " + yValue + " " + xValue + " 00189 16 09\r\n");
                for (int j = 0; j < allLines.Count; j++)
                {
                    //暂时没有CO数据
                    //allLines[j].Append("999999");
                    if (j != 7)
                    {
                        allLines[j].Append("9999");
                        allLines[j].Append(" 9");
                        allLines[j].Append(" 999999");
                    }
                    //24小时AQI预报值与等级
                    else
                    {
                        allLines[j].Append(AQIjisuan(Convert.ToInt32(AQIValue)));
                        allLines[j].Append(" " + CalculateAQLLevelNo(AQIValue));

                        allLines[j].Append(" " + FirstItemChange(aqiItem));
                    }
                    if (j < allLines.Count - 1)
                    {
                        sb.Append(allLines[j].ToString() + "\r\n");
                    }
                    else
                    {
                        sb.Append(allLines[j].ToString());
                    }
                }
            }
            return sb.ToString();
        }

        //查询各个站点用语预报文本文件的AQI值和ITEMID
        public DataTable GetReportTextAQIValueAndItemIDTable(string forecastDateTime, string siteID)
        {
            if (siteID != "" && forecastDateTime != "")
            {
                string strAQISQL = "select m.* from  ( select Max(AQI) AS AQI,Site From(select Site,LST,ITEMID,AQI from T_ForecastSite  WHERE  Site ='" + siteID + "' AND durationID=7 AND LST='" + forecastDateTime + "' and ITEMID <>5) result GROUP BY result.Site  ) t , ( select Site,LST,ITEMID,AQI ,[ForecastDate]from T_ForecastSite  WHERE  Site ='" + siteID + "' AND durationID=7 AND LST='" + forecastDateTime + "' and ITEMID <>5) m where t.AQI=m.AQI and t.Site=m.Site";
                return m_Database.GetDataTable(strAQISQL);
            }
            return null;
        }

        public DataTable GetReportTextAQIValueAndItemIDTableNew(string forecastDateTime,string maxdate, string siteID)
        {
            if (siteID != "" && forecastDateTime != "" && maxdate!="")
            {
                string strAQISQL = "select m.* from  ( select Max(AQI) AS AQI,Site From(select Site,LST,ITEMID,AQI from T_ForecastSite  WHERE  Site ='" + siteID + "' AND durationID=7 AND ForecastDate='" + maxdate + "' AND LST='" + forecastDateTime + "' and ITEMID <>5) result GROUP BY result.Site  ) t , ( select Site,LST,ITEMID,AQI ,[ForecastDate]from T_ForecastSite  WHERE  Site ='" + siteID + "' AND durationID=7  AND ForecastDate='" + maxdate + "' AND LST='" + forecastDateTime + "' and ITEMID <>5) m where t.AQI=m.AQI and t.Site=m.Site";
                return m_Database.GetDataTable(strAQISQL);
            }
            return null;
        }

        public string UpLoadMsgTextCopy(string msgText)
        {
            DateTime dtNow = DateTime.Now;
            string strFilePrefix = "SMS_3_AQI";
            string strFileName = strFilePrefix + dtNow.ToString("yyyyMMddHHmmss") + ".txt";
            //string strFtpURL = "172.21.107.24";
            //string strUser = "SmsRequest";
            //string strPwd = "aa9dsMTr";
            string strFtpURL = "";
            string strUser = "";
            string strPwd = "";
            string strMsgFtpString = ConfigurationManager.AppSettings["MsgFTP"];
            if (strMsgFtpString != "")
            {
                string[] msgFTPValues = strMsgFtpString.Split(';');
                strFtpURL = msgFTPValues[0];
                strUser = msgFTPValues[1];
                strPwd = msgFTPValues[2];
            }
            //存储上传到短信服务器上的文本内容
            string strTotalFileContent = "";
            string strPhoneNums = ConfigurationManager.AppSettings["MsgPhoneNumber"];
            string[] nums = strPhoneNums.Split(',');
            if (nums.Length > 0)
            {
                for (int i = 0; i < nums.Length; i++)
                {
                    strTotalFileContent += nums[i];
                    strTotalFileContent += "\t" + msgText + "\r\n";
                }
            }
            string strMsgResult = UpLoadTxtToFTP(strFtpURL, strFileName, strUser, strPwd, strTotalFileContent);
            return strMsgResult;
        }

        public string UpLoadMsgText(string msgText,string userName,string tempFilePath)
        {
            DateTime dtNow = DateTime.Now;
            string strFilePrefix_LianTong = "SMS_3_AQI";
            string strFileName_LianTong = strFilePrefix_LianTong + dtNow.ToString("yyyyMMddHHmmss") + ".txt";

            string strFilePrefix_YiDong = "SMS_1_AQI";
            string strFileName_YiDong = strFilePrefix_YiDong + dtNow.ToString("yyyyMMddHHmmss") + ".txt";
            //string strFtpURL = "172.21.107.24";
            //string strUser = "SmsRequest";
            //string strPwd = "aa9dsMTr";
            string strFtpURL = "";
            string strUser = "";
            string strPwd = "";
            string strMsgFtpString = ConfigurationManager.AppSettings["MsgFTP"];
            if (strMsgFtpString != "")
            {
                string[] msgFTPValues = strMsgFtpString.Split(';');
                strFtpURL = msgFTPValues[0];
                strUser = msgFTPValues[1];
                strPwd = msgFTPValues[2];
            }
            //存储上传到短信服务器上的文本内容
            string strTotalFileContent = "";
            string strPhoneNums_LianTong = ConfigurationManager.AppSettings["MsgPhoneNumberLianTong"];
            string strPhoneNums_YiDong = ConfigurationManager.AppSettings["MsgPhoneNumberYiDong"];
            string[] nums = strPhoneNums_LianTong.Split(',');
            if (nums.Length > 0)
            {
                for (int i = 0; i < nums.Length; i++)
                {
                    strTotalFileContent += nums[i];
                    strTotalFileContent += "\t" + msgText + "\r\n";
                }
            }
            string strMsgResult_LianTong = UpLoadTxtToFTP(strFtpURL, strFileName_LianTong, strUser, strPwd, strTotalFileContent);

            nums = strPhoneNums_YiDong.Split(',');
            strTotalFileContent = "";
            if (nums.Length > 0)
            {
                for (int i = 0; i < nums.Length; i++)
                {
                    strTotalFileContent += nums[i];
                    strTotalFileContent += "\t" + msgText + "\r\n";
                }
            }
            string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string strMsgResult_YiDong = UpLoadTxtToFTP(strFtpURL, strFileName_YiDong, strUser, strPwd, strTotalFileContent);
            string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string strMsgResult = "fail";
            if (strMsgResult_LianTong == "success" && strMsgResult_YiDong == "success")
            {
                strMsgResult = "success";
            }
            string strIP = HttpClientHelper.GetIP();
            string strPubState = (strMsgResult == "success") ? "0" : "1";
            InsertFTPUpLoadLogNew("AQI分时段预报短信模板", "短信", strStart, strEnd, strPubState, "ftp://" + strFtpURL, userName, strIP, strEnd, "Type", tempFilePath);
            return strMsgResult;
        }

        //真实用数据表中的号码表发送FTP的方法
        public string UpLoadMsgTextRealPast(string msgText)
        {
            DateTime dtNow = DateTime.Now;
            //联通号码
            string strFilePrefix_LianTong = "SMS_3_AQI";
            string strFileName_LianTong = strFilePrefix_LianTong + dtNow.ToString("yyyyMMddHHmmss") + ".txt";
            //移动号码
            string strFilePrefix_YiDong = "SMS_1_AQI";
            string strFileName_YiDong = strFilePrefix_YiDong + dtNow.ToString("yyyyMMddHHmmss") + ".txt";
            //电信号码
            string strFilePrefix_DianXin = "SMS_2_AQI";
            string strFileName_DianXin = strFilePrefix_DianXin + dtNow.ToString("yyyyMMddHHmmss") + ".txt";
            //string strFtpURL = "172.21.107.24";
            //string strUser = "SmsRequest";
            //string strPwd = "aa9dsMTr";
            string strFtpURL = "";
            string strUser = "";
            string strPwd = "";
            string strMsgFtpString = ConfigurationManager.AppSettings["MsgFTP"];
            if (strMsgFtpString != "")
            {
                string[] msgFTPValues = strMsgFtpString.Split(';');
                strFtpURL = msgFTPValues[0];
                strUser = msgFTPValues[1];
                strPwd = msgFTPValues[2];
            }
            //存储上传到短信服务器上的文本内容
            string strTotalFileContent_LianTong = "";
            string strTotalFileContent_YiDong = "";
            string strTotalFileContent_DianXin = "";

            string strPhoneNumsSQL = "select number,flag from T_Messgae";
            DataTable dtPhones = m_Database.GetDataTable(strPhoneNumsSQL);
            if (dtPhones.Rows.Count > 0)
            {

                for (int i = 0; i < dtPhones.Rows.Count; i++)
                {
                    if (dtPhones.Rows[i]["flag"].ToString().Trim() == "3")
                    {
                        strTotalFileContent_LianTong += dtPhones.Rows[i]["number"].ToString();
                        strTotalFileContent_LianTong += "\t" + msgText + "\r\n";
                    }
                    else if (dtPhones.Rows[i]["flag"].ToString().Trim() == "1")
                    {
                        strTotalFileContent_YiDong += dtPhones.Rows[i]["number"].ToString();
                        strTotalFileContent_YiDong += "\t" + msgText + "\r\n";
                    }
                    else if (dtPhones.Rows[i]["flag"].ToString().Trim() == "2")
                    {
                        strTotalFileContent_DianXin += dtPhones.Rows[i]["number"].ToString();
                        strTotalFileContent_DianXin += "\t" + msgText + "\r\n";
                    }
                }
            }
            string strMsgResult_LianTong = "";
            string strMsgResult_YiDong = "";
            string strMsgResult_DianXin = "";
            List<string> results = new List<string>();
            if (strTotalFileContent_LianTong != "")
            {
                strMsgResult_LianTong = UpLoadTxtToFTP(strFtpURL, strFileName_LianTong, strUser, strPwd, strTotalFileContent_LianTong);
                results.Add(strMsgResult_LianTong);
            }
            if (strTotalFileContent_YiDong != "")
            {
                strMsgResult_YiDong = UpLoadTxtToFTP(strFtpURL, strFileName_YiDong, strUser, strPwd, strTotalFileContent_YiDong);
                results.Add(strMsgResult_YiDong);
            }
            if (strTotalFileContent_DianXin != "")
            {
                strMsgResult_DianXin = UpLoadTxtToFTP(strFtpURL, strFileName_DianXin, strUser, strPwd, strTotalFileContent_DianXin);
                results.Add(strMsgResult_DianXin);
            }
            string strMsgResult = "success";
            for (int j = 0; j < results.Count; j++)
            {
                if (results[j] == "fail")
                {
                    strMsgResult = "fail";
                }
            }
            return strMsgResult;
        }

        public string UpLoadMsgTextRealCopy(string msgText, string userName, string tempFilePath)
        {
            DateTime dtNow = DateTime.Now;
            //联通号码
            string strFilePrefix_LianTong = "SMS_3_AQI";
            string strFileName_LianTong = strFilePrefix_LianTong + dtNow.ToString("yyyyMMddHHmmss") + ".txt";
            //移动号码
            string strFilePrefix_YiDong = "SMS_1_AQI";
            string strFileName_YiDong = strFilePrefix_YiDong + dtNow.ToString("yyyyMMddHHmmss") + ".txt";
            //电信号码
            string strFilePrefix_DianXin = "SMS_2_AQI";
            string strFileName_DianXin = strFilePrefix_DianXin + dtNow.ToString("yyyyMMddHHmmss") + ".txt";
            //string strFtpURL = "172.21.107.24";
            //string strUser = "SmsRequest";
            //string strPwd = "aa9dsMTr";
            string strFtpURL = "";
            string strUser = "";
            string strPwd = "";
            string strMsgFtpString = ConfigurationManager.AppSettings["MsgFTP"];
            if (strMsgFtpString != "")
            {
                string[] msgFTPValues = strMsgFtpString.Split(';');
                strFtpURL = msgFTPValues[0];
                strUser = msgFTPValues[1];
                strPwd = msgFTPValues[2];
            }
            //存储上传到短信服务器上的文本内容
            string strTotalFileContent_LianTong = "";
            string strTotalFileContent_YiDong = "";
            string strTotalFileContent_DianXin = "";
            string strPhoneNumsSQL = "select number,flag from T_Messgae";
            //string strPhoneNumsSQL = "select number,flag from T_Messgae_Test";
            DataTable dtPhones = m_Database.GetDataTable(strPhoneNumsSQL);
            if (dtPhones.Rows.Count > 0)
            {
                for (int i = 0; i < dtPhones.Rows.Count; i++)
                {
                    if (dtPhones.Rows[i]["flag"].ToString().Trim() == "3")
                    {
                        strTotalFileContent_LianTong += dtPhones.Rows[i]["number"].ToString();
                        strTotalFileContent_LianTong += "\t" + msgText + "\r\n";
                    }
                    else if (dtPhones.Rows[i]["flag"].ToString().Trim() == "1")
                    {
                        strTotalFileContent_YiDong += dtPhones.Rows[i]["number"].ToString();
                        strTotalFileContent_YiDong += "\t" + msgText + "\r\n";
                    }
                    else if (dtPhones.Rows[i]["flag"].ToString().Trim() == "2")
                    {
                        strTotalFileContent_DianXin += dtPhones.Rows[i]["number"].ToString();
                        strTotalFileContent_DianXin += "\t" + msgText + "\r\n";
                    }
                }
            }
            string strMsgResult_LianTong = "";
            string strMsgResult_YiDong = "";
            string strMsgResult_DianXin = "";
            List<string> results = new List<string>();
            string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            if (strTotalFileContent_LianTong != "")
            {
                strMsgResult_LianTong = UpLoadTxtToFTP(strFtpURL, strFileName_LianTong, strUser, strPwd, strTotalFileContent_LianTong);
                results.Add(strMsgResult_LianTong);
            }
            if (strTotalFileContent_YiDong != "")
            {
                strMsgResult_YiDong = UpLoadTxtToFTP(strFtpURL, strFileName_YiDong, strUser, strPwd, strTotalFileContent_YiDong);
                results.Add(strMsgResult_YiDong);
            }
            if (strTotalFileContent_DianXin != "")
            {
                strMsgResult_DianXin = UpLoadTxtToFTP(strFtpURL, strFileName_DianXin, strUser, strPwd, strTotalFileContent_DianXin);
                results.Add(strMsgResult_DianXin);
            }
            string strMsgResult = "success";
            for (int j = 0; j < results.Count; j++)
            {
                if (results[j] == "fail")
                {
                    strMsgResult = "fail";
                }
            }                        
            string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");            
            string strIP = HttpClientHelper.GetIP();
            string strPubState = (strMsgResult == "success") ? "0" : "1";
            //ftp文本保存临时路径，用语发布日志预览
            string strFileName = "AQIPeriod_Msg.txt";
            string strFtpTempSavePath = ConfigurationManager.AppSettings["FtpUploadTxtTempPath"].ToString();
            if (!Directory.Exists(strFtpTempSavePath))
            {
                Directory.CreateDirectory(strFtpTempSavePath);
            }
            string strFullFileName = strFtpTempSavePath + DateTime.Now.ToString("yyyyMMddHHmmss") + strFileName;
            using (FileStream tempFs = new FileStream(strFullFileName, FileMode.OpenOrCreate))
            {
                StreamWriter sw = new StreamWriter(tempFs);
                sw.Write(msgText);
                sw.Close();
            }

            InsertFTPUpLoadLogNew("AQI分时段预报短信模板", "短信", strStart, strEnd, strPubState, "ftp://" + strFtpURL, userName, strIP, strEnd, "Type", strFullFileName);
            return strMsgResult;
        }

        public string UpLoadMsgTextReal(string msgText, string userName, string tempFilePath)
        {
            DateTime dtNow = DateTime.Now;
            //联通号码
            string strFilePrefix_LianTong = "SMS_3_AQI";
            string strFileName_LianTong = strFilePrefix_LianTong + dtNow.ToString("yyyyMMddHHmmss") + ".txt";
            //移动号码
            string strFilePrefix_YiDong = "SMS_1_AQI";
            string strFileName_YiDong = strFilePrefix_YiDong + dtNow.ToString("yyyyMMddHHmmss") + ".txt";
            //电信号码
            string strFilePrefix_DianXin = "SMS_2_AQI";
            string strFileName_DianXin = strFilePrefix_DianXin + dtNow.ToString("yyyyMMddHHmmss") + ".txt";
            //string strFtpURL = "172.21.107.24";
            //string strUser = "SmsRequest";
            //string strPwd = "aa9dsMTr";
            string strFtpURL = "";
            string strUser = "";
            string strPwd = "";
            string strMsgFtpString = ConfigurationManager.AppSettings["MsgFTP"];
            if (strMsgFtpString != "")
            {
                string[] msgFTPValues = strMsgFtpString.Split(';');
                strFtpURL = msgFTPValues[0];
                strUser = msgFTPValues[1];
                strPwd = msgFTPValues[2];
            }
            //存储上传到短信服务器上的文本内容
            string strTotalFileContent_LianTong = "";
            string strTotalFileContent_YiDong = "";
            string strTotalFileContent_DianXin = "";
            string strPhoneNumsSQL = "select number,flag from T_Messgae";
            //string strPhoneNumsSQL = "select number,flag from T_Messgae_Test";
            DataTable dtPhones = m_Database.GetDataTable(strPhoneNumsSQL);
            if (dtPhones.Rows.Count > 0)
            {
                for (int i = 0; i < dtPhones.Rows.Count; i++)
                {
                    if (dtPhones.Rows[i]["flag"].ToString().Trim() == "3")
                    {
                        strTotalFileContent_LianTong += dtPhones.Rows[i]["number"].ToString();
                        strTotalFileContent_LianTong += "\t" + msgText + "\r\n";
                    }
                    else if (dtPhones.Rows[i]["flag"].ToString().Trim() == "1")
                    {
                        strTotalFileContent_YiDong += dtPhones.Rows[i]["number"].ToString();
                        strTotalFileContent_YiDong += "\t" + msgText + "\r\n";
                    }
                    else if (dtPhones.Rows[i]["flag"].ToString().Trim() == "2")
                    {
                        strTotalFileContent_DianXin += dtPhones.Rows[i]["number"].ToString();
                        strTotalFileContent_DianXin += "\t" + msgText + "\r\n";
                    }
                }
            }
            string strMsgResult_LianTong = "";
            string strMsgResult_YiDong = "";
            string strMsgResult_DianXin = "";
            List<string> results = new List<string>();
            string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            if (strTotalFileContent_LianTong != "")
            {
                strMsgResult_LianTong = UpLoadTxtToFTP(strFtpURL, strFileName_LianTong, strUser, strPwd, strTotalFileContent_LianTong);
                results.Add(strMsgResult_LianTong);
            }
            if (strTotalFileContent_YiDong != "")
            {
                strMsgResult_YiDong = UpLoadTxtToFTP(strFtpURL, strFileName_YiDong, strUser, strPwd, strTotalFileContent_YiDong);
                results.Add(strMsgResult_YiDong);
            }
            if (strTotalFileContent_DianXin != "")
            {
                strMsgResult_DianXin = UpLoadTxtToFTP(strFtpURL, strFileName_DianXin, strUser, strPwd, strTotalFileContent_DianXin);
                results.Add(strMsgResult_DianXin);
            }
            string strMsgResult = "success";
            for (int j = 0; j < results.Count; j++)
            {
                if (results[j] == "fail")
                {
                    strMsgResult = "fail";
                }
            }
            string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string strIP = HttpClientHelper.GetIP();
            string strPubState = (strMsgResult == "success") ? "0" : "1";
            //ftp文本保存临时路径，用语发布日志预览
            string strFileName = "AQIPeriod_Msg.txt";
            string strFtpTempSavePath = ConfigurationManager.AppSettings["FtpUploadTxtTempPath"].ToString();
            if (!Directory.Exists(strFtpTempSavePath))
            {
                Directory.CreateDirectory(strFtpTempSavePath);
            }
            string strFullFileName = strFtpTempSavePath + DateTime.Now.ToString("yyyyMMddHHmmss") + strFileName;
            using (FileStream tempFs = new FileStream(strFullFileName, FileMode.OpenOrCreate))
            {
                StreamWriter sw = new StreamWriter(tempFs);
                sw.Write(msgText);
                sw.Close();
            }

            InsertFTPUpLoadLogNew("AQI分时段预报短信模板", "短信", strStart, strEnd, strPubState, "ftp://" + strFtpURL, userName, strIP, strEnd, "Type", strFullFileName);
            return strMsgResult;
        }

        //根据新的短信上传文档（2016年3月3日）
        public string UpLoadMsgTextRealNewVersion(string msgText, string userName, string tempFilePath)
        {
            //上传到FTP的文本文件名
            string strUploadFileName = "NewMsg.txt";
            //上传的文本内容
            string strUploadTxtContent = "";
            strUploadTxtContent += "<Format value=\"DUSI\"/>" + "\r\n";
            strUploadTxtContent += "<Information value=\"" + msgText + "\"/>" + "\r\n";
            strUploadTxtContent += "<MobileNo>" + "\r\n";
            
            string strFtpURL = "";
            string strUser = "";
            string strPwd = "";
            string strMsgFtpString = ConfigurationManager.AppSettings["MsgFTP"];
            if (strMsgFtpString != "")
            {
                string[] msgFTPValues = strMsgFtpString.Split(';');
                strFtpURL = msgFTPValues[0];
                strUser = msgFTPValues[1];
                strPwd = msgFTPValues[2];
            }

            string strPhoneNumsSQL = "select number,flag from T_Messgae";
            //string strPhoneNumsSQL = "select number,flag from T_Messgae_Test";
            DataTable dtPhones = m_Database.GetDataTable(strPhoneNumsSQL);
            if (dtPhones.Rows.Count > 0)
            {
                for (int i = 0; i < dtPhones.Rows.Count; i++)
                {
                    strUploadTxtContent += dtPhones.Rows[i]["number"].ToString() + "\r\n";                    
                }
            }
            strUploadTxtContent += "</MobileNo>";


            //ftp文本保存临时路径，用语发布日志预览
            string strFileName = "AQIPeriod_Msg.txt";
            string strFtpTempSavePath = ConfigurationManager.AppSettings["FtpUploadTxtTempPath"].ToString();
            if (!Directory.Exists(strFtpTempSavePath))
            {
                Directory.CreateDirectory(strFtpTempSavePath);
            }
            string strFullFileName = strFtpTempSavePath + DateTime.Now.ToString("yyyyMMddHHmmss") + strFileName;
            using (FileStream tempFs = new FileStream(strFullFileName, FileMode.OpenOrCreate))
            {
                StreamWriter sw = new StreamWriter(tempFs);
                sw.Write(msgText);
                sw.Close();
            }

            //将文件保存，用于计算MD5值
            string strCreateTimeString = DateTime.Now.ToString("yyyyMMddHHmmss");
            string strMD5TempFileName = strFtpTempSavePath + "SMS" + "-" + "20012" + "-" + strCreateTimeString + ".txt";
            using (FileStream tempFs = new FileStream(strMD5TempFileName, FileMode.OpenOrCreate))
            {
                StreamWriter sw = new StreamWriter(tempFs,Encoding.Default);
                sw.Write(strUploadTxtContent);
                sw.Close();
            }

            //计算文件的MD5
            FileStream tempFile = new FileStream(strMD5TempFileName, FileMode.Open);
            System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(tempFile);
            tempFile.Close();
 
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < retVal.Length; i++)
            {
                sb.Append(retVal[i].ToString("x2"));
            }
           
            //系统认证码
            string strSysID =  ConfigurationManager.AppSettings["MsgSysID"];
             //中间生成的文件名，用语计算最终的MD5码
            string strTempFileName = "SMS" + "-" + "20012" + "-" + strCreateTimeString + "-" + strSysID + "-" + sb.ToString().ToUpper();

            string strfinalMD5 = GetMd5Hash(strTempFileName);

            string strFinalFileName = "SMS" + "-" + "20012" + "-" + strCreateTimeString + "-" + strfinalMD5.ToUpper() + ".txt";

            string strFinalFileFullPath = strFtpTempSavePath + strFinalFileName;

            if (System.IO.File.Exists(strMD5TempFileName))
            {
                System.IO.FileInfo file = new System.IO.FileInfo(strMD5TempFileName);
                file.MoveTo(strFinalFileFullPath);
            }

            string strMsgResult = "1";
            string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            //strMsgResult = UpLoadTxtToFTPNewVersionMsg("10.228.19.113:21", strFinalFileName, "20012", "Hj&12%200M6#", strUploadTxtContent);
            //strMsgResult = UpLoadTxtToFTPNewVersionMsg("127.0.0.1/Msg", strFinalFileName, "Admin", "gigh06508012", strUploadTxtContent);

            
            try
            {
                Ftp ftp = new Ftp("10.228.19.113:21", "20012", "Hj&12%200M6#");
                //Ftp ftp = new Ftp("127.0.0.1/Msg", "Admin", "gigh06508012");
                ftp.Upload(strFinalFileFullPath, strFinalFileName);
                strMsgResult = "success";
            }
            catch
            { strMsgResult = "fail"; }

             
            string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string strIP = HttpClientHelper.GetIP();
            string strPubState = (strMsgResult == "success") ? "0" : "1";

            InsertFTPUpLoadLogNew("AQI分时段预报短信模板", "短信", strStart, strEnd, strPubState, "ftp://" + strFtpURL, userName, strIP, strEnd, "Type", strFullFileName);
            return strMsgResult;
        }

        public string UpLoadMsgTextNew(string msgText, string userName)
        {
            DateTime dtNow = DateTime.Now;
            string strFilePrefix_1 = "SMS_1_AQI";
            string strFileName_1 = strFilePrefix_1 + dtNow.ToString("yyyyMMddHHmmss") + ".txt";

            string strFilePrefix_3 = "SMS_1_AQI";
            string strFileName_3 = strFilePrefix_3 + dtNow.ToString("yyyyMMddHHmmss") + ".txt";
            //string strFtpURL = "172.21.107.24";
            //string strUser = "SmsRequest";
            //string strPwd = "aa9dsMTr";
            string strFtpURL = "";
            string strUser = "";
            string strPwd = "";
            //查询发送手机号码


            string strPhoneSQL = "SELECT number,flag from dbo.T_Messgae";
            string strMsgFtpString = ConfigurationManager.AppSettings["MsgFTP"];
            if (strMsgFtpString != "")
            {
                string[] msgFTPValues = strMsgFtpString.Split(';');
                strFtpURL = msgFTPValues[0];
                strUser = msgFTPValues[1];
                strPwd = msgFTPValues[2];
            }
            //存储上传到短信服务器上的文本内容
            string strTotalFileContent_1 = "";
            string strTotalFileContent_3 = "";
            string strPhoneNums = ConfigurationManager.AppSettings["MsgPhoneNumber"];
            DataTable dtPhone = m_Database.GetDataTable(strPhoneSQL);
            if (dtPhone.Rows.Count > 0)
            {
                for (int i = 0; i < dtPhone.Rows.Count; i++)
                {
                    if (dtPhone.Rows[i]["flag"].ToString() == "1")
                    {
                        strTotalFileContent_1 += dtPhone.Rows[i]["number"];
                        strTotalFileContent_1 += "\t" + msgText + "\r\n";
                    }
                    else if (dtPhone.Rows[i]["flag"].ToString() == "3")
                    {
                        strTotalFileContent_3 += dtPhone.Rows[i]["number"];
                        strTotalFileContent_3 += "\t" + msgText + "\r\n";
                    }

                }
            }
            string strMsgResult_1 = UpLoadTxtToFTP(strFtpURL, strFileName_1, strUser, strPwd, strTotalFileContent_1);
            string strMsgResult_3 = UpLoadTxtToFTP(strFtpURL, strFileName_3, strUser, strPwd, strTotalFileContent_3);
            return strMsgResult_1 + "-" + strMsgResult_3;
        }

        public string UpLoadAQIPeriodTextAndMsg(string ftpString, string fileDate, string functionName, string txtContent, string txtMsg, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //将文本文件上传到ftp

            //AQI分时段发布时间（每个功能不一样）
            string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
            //最终修改时间
            string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");

            //string strFtpResult = UpLoadTxtFtpLatest(ftpString, fileDate, functionName, txtContent, userName);
            string strFtpResult = UpLoadTxtFtpLatestForAQIPeriod(ftpString, fileDate, functionName, txtContent, userName);
            //如果还没发布，上传短信内容
            string strMsgResult = "";
            if (IsPublished(functionName, strDate) == false)
            {
                if (strFtpResult.Contains("success"))
                {
                    string strTxtTempFile = strFtpResult.Split('&')[1];
                    strMsgResult = UpLoadMsgText(txtMsg, userName, strTxtTempFile);
                }
                if (strFtpResult.Contains("success") && strMsgResult == "success")
                {
                    InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                    //InsertIntoStateTable(functionName, strRcdTime, "3");
                    return "success";
                }
                else if (strFtpResult == "fail" && strMsgResult == "fail")
                {
                    InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "2", "2");
                    //InsertIntoStateTable(functionName, strRcdTime, "2");
                    return "fail";
                }
                else
                {
                    InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                    //InsertIntoStateTable(functionName, strRcdTime, "4");
                    return "less";
                }
            }

            if (strFtpResult.Contains("success"))
            {
                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                //InsertIntoStateTable(functionName, strRcdTime, "3");
            }
            else if (strFtpResult == "fail")
            {
                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "2", "2");
                //InsertIntoStateTable(functionName, strRcdTime, "2");
            }
            else if (strFtpResult == "less")
            {
                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                //InsertIntoStateTable(functionName, strRcdTime, "4");
            }

            return strFtpResult;
        }
       
        public string UpLoadAQIPeriodTextAndMsgRealPast(string ftpString, string fileDate, string functionName, string txtContent, string txtMsg, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //将文本文件上传到ftp

            //AQI分时段发布时间（每个功能不一样）
            string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
            //最终修改时间
            string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");

            //string strFtpResult = UpLoadTxtFtpLatest(ftpString, fileDate, functionName, txtContent, userName);
            //读取的在服务器上存储的AQI分时段预报文本
            string strSavedText = GetAQIPeriodTextContent("text");
            if (strSavedText != "" && strSavedText != null)
            {
                txtContent = strSavedText;
            }
            string strFtpResult = UpLoadTxtFtpLatestForAQIPeriod(ftpString, fileDate, functionName, txtContent, userName);
            //如果还没发布，上传短信内容
            string strMsgResult = "";
            if (IsPublished(functionName, strDate) == false)
            {
                if (strFtpResult.Contains("success"))
                {
                    string strTxtTempFile = strFtpResult.Split('&')[1];
                    string strSavedMsgText = GetAQIPeriodTextContent("msg");
                    if (strSavedMsgText != "" && strSavedMsgText != null)
                    {
                        txtMsg = strSavedMsgText;
                    }
                    strMsgResult = UpLoadMsgTextReal(txtMsg, userName, strTxtTempFile);
                }
                if (strFtpResult.Contains("success") && strMsgResult == "success")
                {
                    InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                    //InsertIntoStateTable(functionName, strRcdTime, "3");
                    return "success";
                }
                else if (strFtpResult == "fail" && strMsgResult == "fail")
                {
                    InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "2", "2");
                    //InsertIntoStateTable(functionName, strRcdTime, "2");
                    return "fail";
                }
                else
                {
                    InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                    //InsertIntoStateTable(functionName, strRcdTime, "4");
                    return "less";
                }
            }

            if (strFtpResult.Contains("success"))
            {
                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                //InsertIntoStateTable(functionName, strRcdTime, "3");
            }
            else if (strFtpResult == "fail")
            {
                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "2", "2");
                //InsertIntoStateTable(functionName, strRcdTime, "2");
            }
            else if (strFtpResult == "less")
            {
                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                //InsertIntoStateTable(functionName, strRcdTime, "4");
            }

            return strFtpResult;
        }

        public string UpLoadAQIPeriodTextAndMsgRealCopy(string ftpString, string fileDate, string functionName, string txtContent, string txtMsg, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //将文本文件上传到ftp

            //AQI分时段发布时间（每个功能不一样）
            string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
            //最终修改时间
            string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");

            //string strFtpResult = UpLoadTxtFtpLatest(ftpString, fileDate, functionName, txtContent, userName);
            //读取的在服务器上存储的AQI分时段预报文本
            string strSavedText = GetAQIPeriodTextContent("text");
            if (strSavedText != "" && strSavedText != null)
            {
                txtContent = strSavedText;
            }
            string strFtpResult = UpLoadTxtFtpLatestForAQIPeriod(ftpString, fileDate, functionName, txtContent, userName);
            //如果还没发布，上传短信内容
            string strMsgResult = "";
            if (IsPublished(functionName, strDate) == false)
            {
                if (strFtpResult.Contains("success"))
                {
                    string strTxtTempFile = strFtpResult.Split('&')[1];
                    string strSavedMsgText = GetAQIPeriodTextContent("msg");
                    if (strSavedMsgText != "" && strSavedMsgText != null)
                    {
                        txtMsg = strSavedMsgText;
                    }
                    strMsgResult = UpLoadMsgTextReal(txtMsg, userName, strTxtTempFile);
                }
                if (strFtpResult.Contains("success") && strMsgResult == "success")
                {
                    InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                    //InsertIntoStateTable(functionName, strRcdTime, "3");
                    return "success";
                }
                else if (strFtpResult == "fail" && strMsgResult == "fail")
                {
                    InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "2", "2");
                    //InsertIntoStateTable(functionName, strRcdTime, "2");
                    return "fail";
                }
                else
                {
                    InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                    //InsertIntoStateTable(functionName, strRcdTime, "4");
                    return "less";
                }
            }

            if (strFtpResult.Contains("success"))
            {
                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                //InsertIntoStateTable(functionName, strRcdTime, "3");
            }
            else if (strFtpResult == "fail")
            {
                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "2", "2");
                //InsertIntoStateTable(functionName, strRcdTime, "2");
            }
            else if (strFtpResult == "less")
            {
                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                //InsertIntoStateTable(functionName, strRcdTime, "4");
            }

            return strFtpResult;
        }

        public string UpLoadAQIPeriodTextAndMsgReal(string ftpString, string fileDate, string functionName, string txtContent, string txtMsg, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //将文本文件上传到ftp

            //AQI分时段发布时间（每个功能不一样）
            string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
            //最终修改时间
            string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");

            //string strFtpResult = UpLoadTxtFtpLatest(ftpString, fileDate, functionName, txtContent, userName);
            //读取的在服务器上存储的AQI分时段预报文本
            //string strSavedText = GetAQIPeriodTextContent("text");
            //if (strSavedText != "" && strSavedText != null)
            //{
            //    txtContent = strSavedText;
            //}
            string strFtpResult = UpLoadTxtFtpLatestForAQIPeriod(ftpString, fileDate, functionName, txtContent, userName);
            //如果还没发布，上传短信内容
            string strMsgResult = "";
            if (IsPublished(functionName, strDate) == false)
            {
                if (strFtpResult.Contains("success"))
                {
                    string strTxtTempFile = strFtpResult.Split('&')[1];
                    //string strSavedMsgText = GetAQIPeriodTextContent("msg");
                    //if (strSavedMsgText != "" && strSavedMsgText != null)
                    //{
                    //    txtMsg = strSavedMsgText;
                    //}
                   
                    //strMsgResult = UpLoadMsgTextReal(txtMsg, userName, strTxtTempFile);
                    strMsgResult = UpLoadMsgTextRealNewVersion(txtMsg, userName, strTxtTempFile);
                        
                }
                if (strFtpResult.Contains("success") && strMsgResult == "success")
                {
                    InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                    //InsertIntoStateTable(functionName, strRcdTime, "3");
                    return "success";
                }
                else if (strFtpResult == "fail" && strMsgResult == "fail")
                {
                    InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "2", "2");
                    //InsertIntoStateTable(functionName, strRcdTime, "2");
                    return "fail";
                }
                else
                {
                    InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                    //InsertIntoStateTable(functionName, strRcdTime, "4");
                    return "less";
                }
            }

            if (strFtpResult.Contains("success"))
            {
                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                //InsertIntoStateTable(functionName, strRcdTime, "3");
            }
            else if (strFtpResult == "fail")
            {
                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "2", "2");
                //InsertIntoStateTable(functionName, strRcdTime, "2");
            }
            else if (strFtpResult == "less")
            {
                InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                //InsertIntoStateTable(functionName, strRcdTime, "4");
            }

            return strFtpResult;
        }

        //将AQI分区预报word文档上传到FTP
        public string UpLoadAQIAreaWordToFTP(string ftpWordString, string cellsContent, string fileDate, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            if (cellsContent == "" || cellsContent == null)
            {
                return "文本内容不能为空！";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";

            if (ftpWordString != "")
            {
                string[] ftpList = { ftpWordString };

                if (ftpWordString.IndexOf(';') > 0)
                {
                    ftpList = ftpWordString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                else if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                else if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                else if (strFileName.Contains("SHMM"))
                                {
                                    strDate = (date.Month > 9) ? date.Month.ToString() : ("0" + date.Month.ToString());
                                    strFileName = strFileName.Replace("MM", strDate);
                                }
                                else if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }

                                //文件后缀名
                                //string strFileSuffix = strFileName.Split('.')[1];
                                //if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                //{
                                try
                                {
                                    string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    //根据word模板创建word文档存在服务器上
                                    string strNewFileName = "";
                                    string strFtpResult = UploadWordToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, strNewFileName);
                                    string strPubState = strFtpResult == "success" ? "0" : "1";
                                    string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                    //在状态表T_State当中插入记录
                                    string strIP = HttpClientHelper.GetIP();
                                    InsertFTPUpLoadLog("AQI预报", "AQI分区预报Word文档", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                    intSuccessCount++;
                                }
                                catch (Exception e)
                                {
                                    intFailCount++;
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;
                    if (intSuccessCount == ftpList.Length)
                    {
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        return "less";
                    }
                }
            }
            return "fail";
        }

        ////把AQI分区txt文本和word文档上传到FTP



        ////把AQI分区txt文本和word文档上传到FTP,并在固定路径保存预报文本
        public string UploadAQIAreaProductToFTPCopy(string ftpString, string fileDate, string wordModelName, string functionName, string txtContent, string cellsContent, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            string strRcdTime = "";
            string strDeadLineTime = "";
            //获取文本发布名称，需要在服务器固定路径存储一下
            string strTxtFileName = "";
            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                //文件生成时间
                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    //strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", DateTime.Now.ToString("YYYYMMddhhmmss"));
                                }
                                //预报资料起报时间
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMdd2000");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH"))
                                {
                                    strDate = date.ToString("yyyyMMdd14");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                }
                                if (strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }

                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    try
                                    {
                                        strTxtFileName = strFileName;
                                        string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        string strPubState = strFtpResult == "success" ? "0" : "1";
                                        string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        //在状态表T_State当中插入记录
                                        string strIP = HttpClientHelper.GetIP();

                                        InsertFTPUpLoadLog("AQI分区预报", "AQI分区预报文本", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                }
                                else if (strFileSuffix == "doc" || strFileSuffix == "docx")
                                {
                                    try
                                    {
                                        string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //根据word模板创建word文档保存到服务器上，然后在读取上传FTP
                                        string strSourceFileName = CreateAQIAreaWordFromModel(wordModelName, cellsContent, strFileName);
                                        string strFtpResult = UploadWordToFTP(strFTPIPString, strSourceFileName, strFTPUser, strFTPPSW, strFileName);
                                        string strPubState = strFtpResult == "" ? "1" : "0";
                                        string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        //在状态表T_State当中插入记录
                                        string strIP = HttpClientHelper.GetIP();
                                        InsertFTPUpLoadLog("AQI分区预报", "AQI分区Word文档", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                }
                            }
                        }
                    }
                    //将预报文本保存在固定路径
                    try
                    {
                        if (txtContent != "")
                        {
                            txtContent = txtContent.Replace("\n", "\r\n");
                            string strAQIAreaFilePath = ConfigurationManager.AppSettings["AQIAreaReportTextPath"].ToString();
                            string strFullPath = strAQIAreaFilePath + "\\" + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.ToString("yyyyMM");
                            if (!Directory.Exists(strFullPath))
                            {
                                Directory.CreateDirectory(strFullPath);
                            }
                            using (FileStream fs = new FileStream(strFullPath + "\\" + strTxtFileName, FileMode.OpenOrCreate))
                            {
                                StreamWriter sw = new StreamWriter(fs);
                                sw.Write(txtContent);
                                sw.Close();
                            }
                        }
                    }
                    catch { }
                    intFailCount = ftpList.Length - intSuccessCount;
                    strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");
                    strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 19:30:00.000");
                    if (intSuccessCount == ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "3");
                        InsertIntoStateTable("AQIAreaForeFile", strRcdTime, strDeadLineTime, "3", "2");
                        //InsertIntoStateTable("AQIAreaForeFile", strRcdTime, "3");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "4");
                        //表示发布不完全
                        return "less";
                    }
                }

            }
            return "fail";
        }

        public string UploadAQIAreaProductToFTP(string ftpString, string fileDate, string wordModelName, string functionName, string txtContent, string cellsContent, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            string strRcdTime = "";
            string strDeadLineTime = "";
            //获取文本发布名称，需要在服务器固定路径存储一下
            string strTxtFileName = "";

            //ftp文本保存临时路径，用语发布日志预览
            string strFtpTempSavePath = ConfigurationManager.AppSettings["FtpUploadTxtTempPath"].ToString();
            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                //文件生成时间
                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    //strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", DateTime.Now.AddHours(-8).ToString("yyyyMMddhhmmss"));
                                }
                                //预报资料起报时间
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMdd2000");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH"))
                                {
                                    strDate = date.ToString("yyyyMMdd14");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                }
                                if (strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }

                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    string strPubState = "1";
                                    string strStart = "";
                                    string strEnd = "";
                                    string strIP = "";
                                    try
                                    {
                                        strTxtFileName = strFileName;
                                        strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        strPubState = strFtpResult == "success" ? "0" : "1";
                                        strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        //在状态表T_State当中插入记录
                                        strIP = HttpClientHelper.GetIP();

                                        //InsertFTPUpLoadLog("AQI分区预报", "AQI分区预报文本", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;


                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }

                                    //将发布的文本在服务器固定路径存储，用于发布日志预览
                                    if (!Directory.Exists(strFtpTempSavePath))
                                    {
                                        Directory.CreateDirectory(strFtpTempSavePath);
                                    }
                                    StringBuilder sb = new StringBuilder(txtContent);
                                    string strTempContent = sb.ToString().Replace("\n", "\r\n");
                                    using (FileStream tempFs = new FileStream(strFtpTempSavePath + strTxtFileName, FileMode.OpenOrCreate))
                                    {
                                        StreamWriter sw = new StreamWriter(tempFs);
                                        sw.Write(strTempContent);
                                        sw.Close();
                                    }
                                    string strTxtProName = "";
                                    switch (ftpList[i].Split(',')[0])
                                    {
                                        case "NationalOffice":
                                            strTxtProName = "AQI分区指导预报上传国家局";
                                            break;
                                        default:
                                            strTxtProName = "AQI分区指导预报";
                                            break;

                                    }
                                    InsertFTPUpLoadLogNew("AQI分区预报", strTxtProName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strFtpTempSavePath + strTxtFileName);
                                }
                                else if (strFileSuffix == "doc" || strFileSuffix == "docx")
                                {
                                    try
                                    {
                                        string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //根据word模板创建word文档保存到服务器上，然后在读取上传FTP
                                        string strSourceFileName = CreateAQIAreaWordFromModel(wordModelName, cellsContent, strFileName);

                                        string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
                                        //Word文档再服务器上保存的路径
                                        string strWordUrl = strBase + "AQI\\WordProduct\\AQIArea\\" + strSourceFileName;

                                        string strFtpResult = UploadWordToFTP(strFTPIPString, strWordUrl, strFTPUser, strFTPPSW, strFileName);
                                        string strPubState = strFtpResult == "" ? "1" : "0";
                                        string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        string strIP = HttpClientHelper.GetIP();
                                        //InsertFTPUpLoadLog("AQI分区预报", "AQI分区Word文档", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type");

                                        string strWordProName = "";
                                        switch (ftpList[i].Split(',')[0])
                                        {
                                            case "32Down":
                                                strWordProName = "AQI分区指导预报下发各区县";
                                                break;
                                            case "62WebSite":
                                                strWordProName = "AQI分区指导预报下发各区县scuem";
                                                break;
                                            default:
                                                strWordProName = "AQI分区指导预报";
                                                break;
                                        }

                                        InsertFTPUpLoadLogNew("AQI分区预报", strWordProName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strWordUrl);
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                }
                            }
                        }
                    }
                    //将预报文本保存在固定路径
                    try
                    {
                        if (txtContent != "")
                        {
                            txtContent = txtContent.Replace("\n", "\r\n");
                            string strAQIAreaFilePath = ConfigurationManager.AppSettings["AQIAreaReportTextPath"].ToString();
                            string strFullPath = strAQIAreaFilePath + "\\" + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.ToString("yyyyMM");
                            if (!Directory.Exists(strFullPath))
                            {
                                Directory.CreateDirectory(strFullPath);
                            }
                            //保存在固定路径的文件名
                            string strUploadFileName = strTxtFileName.Replace(".TXT","_"+userName+".TXT");
                            using (FileStream fs = new FileStream(strFullPath + "\\" + strUploadFileName, FileMode.OpenOrCreate))
                            {
                                StreamWriter sw = new StreamWriter(fs);
                                sw.Write(txtContent);
                                sw.Close();
                            }
                        }
                    }
                    catch { }
                    intFailCount = ftpList.Length - intSuccessCount;
                    strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");
                    strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 19:30:00.000");
                    if (intSuccessCount == ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "3");
                        InsertIntoStateTable("AQIAreaForeFile", strRcdTime, strDeadLineTime, "3", "2");
                        //InsertIntoStateTable("AQIAreaForeFile", strRcdTime, "3");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "4");
                        //表示发布不完全
                        return "less";
                    }
                }

            }
            return "fail";
        }

        ////将单个word文档上传到一个FTP上
        public string UploadWordToFTPCopy(string strFTPIPString, string strSourceFileName, string strFTPUser, string strFTPPSW, string newFileName)
        {
            try
            {
                string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
                //Word文档再服务器上保存的路径
                string strWordUrl = strBase + "AQI\\WordProduct\\AQIArea" + strSourceFileName;
                Ftp ftp = new Ftp(strFTPIPString, strFTPUser, strFTPPSW);
                ftp.Upload(strWordUrl, newFileName);
                return newFileName;
            }
            catch { }
            return "";
        }

        public string UploadWordToFTP(string strFTPIPString, string strSourceFilePath, string strFTPUser, string strFTPPSW, string newFileName)
        {
            try
            {
                string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
                //Word文档再服务器上保存的路径
                //string strWordUrl = strBase + "AQI\\WordProduct\\AQIArea" + strSourceFileName;
                string strWordUrl = strSourceFilePath;
                Ftp ftp = new Ftp(strFTPIPString, strFTPUser, strFTPPSW);
                ftp.Upload(strWordUrl, newFileName);
                return newFileName;
            }
            catch { }
            return "";
        }

        public string CreateAQIAreaWordFromModelCopy(string modelName, string cellContent, string newFileName)
        {
            //modelName为Word模板的文件名
            string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
            string strModelPath = strBase + "AQI\\WordModel\\" + modelName;
            //服务器上存储路径
            string strNewPath = strBase + "AQI\\WordProduct\\" + newFileName;
            WordHelper wordHelper = new WordHelper(strModelPath);
            Table templateTable = (Table)wordHelper.Document.GetChild(NodeType.Table, 0, true);
            string strBookMark = wordHelper.Document.Range.Bookmarks[0].ToString();
            if (cellContent != "")
            {
                string[] bookmarkParts = cellContent.Split('&');
                if (bookmarkParts.Length > 0)
                {
                    for (int i = 0; i < bookmarkParts.Length; i++)
                    {
                        string[] singleValues = bookmarkParts[i].Split(',');
                        for (int m = 0; m < singleValues.Length; m++)
                        {
                            wordHelper.Replace(singleValues[m].Split(':')[0], singleValues[m].Split(':')[1]);
                        }
                    }
                    Table table = wordHelper.GetTable(0);
                    wordHelper.SaveAs(strNewPath, Aspose.Words.SaveFormat.Doc);
                    return newFileName;
                }
            }
            return "";
        }

        public string CreateAQIAreaWordFromModel(string modelName, string cellContent, string newFileName)
        {
            //modelName为Word模板的文件名
            string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
            string strModelPath = strBase + "AQI\\WordModel\\" + modelName;
            if (!Directory.Exists(strBase + "AQI\\WordProduct\\AQIArea"))
            {
                Directory.CreateDirectory(strBase + "AQI\\WordProduct\\AQIArea");
            }
            //服务器上存储路径
            string strNewPath = strBase + "AQI\\WordProduct\\AQIArea\\" + newFileName;
            WordHelper wordHelper = new WordHelper(strModelPath);
            Table templateTable = (Table)wordHelper.Document.GetChild(NodeType.Table, 0, true);
            string strBookMark = wordHelper.Document.Range.Bookmarks[0].ToString();
            if (cellContent != "")
            {
                string[] bookmarkParts = cellContent.Split('&');
                if (bookmarkParts.Length > 0)
                {
                    for (int i = 0; i < bookmarkParts.Length; i++)
                    {
                        string[] singleValues = bookmarkParts[i].Split(',');
                        for (int m = 0; m < singleValues.Length; m++)
                        {
                            wordHelper.Replace(singleValues[m].Split(':')[0], singleValues[m].Split(':')[1]);
                        }
                    }
                    Table table = wordHelper.GetTable(0);
                    wordHelper.SaveAs(strNewPath, Aspose.Words.SaveFormat.Doc);
                    return newFileName;
                }
            }
            return "";
        }

        //将发布状态存入T_State表
        /// <summary>
        /// 
        /// </summary>
        /// <param name="functionName">产品大类名称</param>
        /// <param name="reTime"></param>
        /// <param name="deadLine"></param>
        /// <param name="state"></param>
        /// <param name="type"></param>
        public void InsertIntoStateTable(string functionName, string reTime, string deadLine, string state, string type)
        {
            string strStateNum = "0";
            if (state == "")
            {
                state = "0";
            }

            //查询当天是否有记录
            string strQueryExistSQL = "select ModuleType, ReTime from T_State where ModuleType='" + functionName + "' AND ReTime='" + reTime + "'";
            DataTable dtQueryExist = m_Database.GetDataTable(strQueryExistSQL);
            deadLine = "";
            string strInsertStateSQL = "INSERT INTO T_State(ModuleType,ReTime,DeadLine,State,Type) VALUES('" + functionName + "','" + reTime + "','" + deadLine + "','" + state + "','" + type + "')";
            //当天未插入状态记录
            if (dtQueryExist.Rows.Count == 0)
            {
                strInsertStateSQL = "INSERT INTO T_State(ModuleType,ReTime,DeadLine,State,Type) VALUES('" + functionName + "','" + reTime + "','" + deadLine + "','" + state + "','" + type + "')";
            }
            //当天已插入记录
            else
            {
                strInsertStateSQL = "delete from T_State where ModuleType='" + functionName + "' AND ReTime='" + reTime + "' " + "INSERT INTO T_State(ModuleType,ReTime,DeadLine,State,Type) VALUES('" + functionName + "','" + reTime + "','" + deadLine + "','" + state + "','" + type + "')";
            }
            m_Database.Execute(strInsertStateSQL);
        }

        //读取各产品发布状态表
        public string ReadStateTable()
        {
            StringBuilder sb = new StringBuilder("{");
            //string strStateJson = "";
            string strReadSQL = "SELECT ModuleType,State From T_State WHERE datediff(day,ReTime,getdate())=0 ";
            //DataTable dtState = m_Database.GetDataTable(strReadSQL);
            DataTable dtState = m_DatabaseJX.GetDataTable(strReadSQL);
            if (dtState.Rows.Count > 0)
            {
                for (int i = 0; i < dtState.Rows.Count; i++)
                {
                    sb.Append("\"" + dtState.Rows[i]["ModuleType"].ToString() + "\":\"" + dtState.Rows[i]["State"].ToString().Trim(' ') + "\",");
                }
                if (sb.Length > 1)
                {
                    sb.Remove(sb.Length - 1, 1);
                    sb.Append("}");
                }
                return sb.ToString();
            }
            return "";
        }

        //将多个AQI落区图片上传FTP       

        public string UploadAQIDropZoneImgsToFTPCopy(string ftpString, string fileDate, string functionName, string imgURLs, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            string[] imgs = null;
            if (imgURLs != "")
            {
                imgs = imgURLs.Split(',');
            }
            else
            {
                return "fail";
            }

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }

                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "GIF" || strFileSuffix == "gif" || strFileSuffix == "jpg")
                                {
                                    string strIP = HttpClientHelper.GetIP();
                                    string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    string strPubState = "1";
                                    string strFtpResult = "fail";
                                    try
                                    {
                                        string sourceFileName = "";
                                        string strItemName = "";
                                        if (imgs.Length > 0)
                                        {
                                            for (int m = 0; m < imgs.Length; m++)
                                            {
                                                sourceFileName = imgs[m].Split(':')[1];
                                                strItemName = imgs[m].Split(':')[0];
                                                StringBuilder sb = new StringBuilder(strFileName);
                                                string strUseFileName = "";
                                                string strTempFileName = sourceFileName.Substring(sourceFileName.LastIndexOf('/') + 1);
                                                string strSourceDate = strTempFileName.Split('_')[2];
                                                //起始日期增加的小时数
                                                string strIntervelHour = strTempFileName.Split('_')[3].Split('.')[0];
                                                if (sourceFileName.Contains("o3_1h") || sourceFileName.Contains("o3_8h"))
                                                {
                                                    strSourceDate = strTempFileName.Split('_')[3];
                                                    strIntervelHour = strTempFileName.Split('_')[4].Split('.')[0];
                                                }


                                                int intHour = 0;
                                                intHour = Convert.ToInt32(strIntervelHour);
                                                DateTime sourceDate = new DateTime(Convert.ToInt32(strSourceDate.Substring(0, 4)), Convert.ToInt32(strSourceDate.Substring(4, 2)), Convert.ToInt32(strSourceDate.Substring(6, 2)), Convert.ToInt32(strSourceDate.Substring(8, 2)), 0, 0);
                                                strUseFileName = sourceDate.ToString("yyyyMMdd20") + "_" + strItemName + "_" + sourceDate.AddHours(intHour).ToString("MMdd") + "." + strTempFileName.Split('.')[1].ToUpper();
                                                //if (strFileName.Contains("要素"))
                                                //{ H_no2_201601292000_048.gif
                                                //    strUseFileName = sb.ToString().Replace("要素", strItemName);
                                                //}
                                                //NO2不上传
                                                if (strItemName != "no2")
                                                {
                                                    strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                    strFtpResult = UpLoadImg(strFTPIPString, strFTPUser, strFTPPSW, sourceFileName, strUseFileName);
                                                    strPubState = strFtpResult == "success" ? "0" : "1";
                                                    string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                    ////在状态表T_State当中插入记录                                       
                                                    InsertFTPUpLoadLog("华东区域预报", strItemName + "落区图", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strUseFileName, userName, strIP, strEnd, "Type");
                                                    intSuccessCount++;
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length * imgs.Length - intSuccessCount;

                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 13:30:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");
                    if (intSuccessCount == ftpList.Length * (imgs.Length - 1))
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "3");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length * (imgs.Length - 1))
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "4");
                        //表示发布不完全
                        return "less";
                    }
                }
            }

            return "fail";
        }

        public string UploadAQIDropZoneImgsToFTP(string ftpString, string fileDate, string functionName, string imgURLs, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            string[] imgs = null;
            if (imgURLs != "")
            {
                imgs = imgURLs.Split(',');
            }
            else
            {
                return "fail";
            }

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }

                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "GIF" || strFileSuffix == "gif" || strFileSuffix == "jpg")
                                {
                                    string strIP = HttpClientHelper.GetIP();
                                    string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    string strPubState = "1";
                                    string strFtpResult = "fail";
                                    try
                                    {
                                        string sourceFileName = "";
                                        string strItemName = "";
                                        if (imgs.Length > 0)
                                        {
                                            for (int m = 0; m < imgs.Length; m++)
                                            {
                                                sourceFileName = imgs[m].Split(':')[1];
                                                strItemName = imgs[m].Split(':')[0];
                                                StringBuilder sb = new StringBuilder(strFileName);
                                                string strUseFileName = "";
                                                string strTempFileName = sourceFileName.Substring(sourceFileName.LastIndexOf('/') + 1);
                                                string strSourceDate = strTempFileName.Split('_')[2];
                                                //起始日期增加的小时数
                                                string strIntervelHour = strTempFileName.Split('_')[3].Split('.')[0];
                                                if (sourceFileName.Contains("o3_1h") || sourceFileName.Contains("o3_8h"))
                                                {
                                                    strSourceDate = strTempFileName.Split('_')[3];
                                                    strIntervelHour = strTempFileName.Split('_')[4].Split('.')[0];
                                                }

                                                int intHour = 0;
                                                intHour = Convert.ToInt32(strIntervelHour);
                                                DateTime sourceDate = new DateTime(Convert.ToInt32(strSourceDate.Substring(0, 4)), Convert.ToInt32(strSourceDate.Substring(4, 2)), Convert.ToInt32(strSourceDate.Substring(6, 2)), Convert.ToInt32(strSourceDate.Substring(8, 2)), 0, 0);
                                                strUseFileName = sourceDate.ToString("yyyyMMdd20") + "_" + strItemName + "_" + sourceDate.AddHours(intHour).ToString("MMdd") + "." + strTempFileName.Split('.')[1].ToUpper();
                                                //if (strFileName.Contains("要素"))
                                                //{ H_no2_201601292000_048.gif
                                                //    strUseFileName = sb.ToString().Replace("要素", strItemName);
                                                //}
                                                //NO2不上传
                                                if (strItemName != "no2")
                                                {
                                                    strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                    strFtpResult = UpLoadImg(strFTPIPString, strFTPUser, strFTPPSW, sourceFileName, strUseFileName);
                                                    strPubState = strFtpResult == "success" ? "0" : "1";
                                                    string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                    //将图片保存在服务器临时路径，发布日志预览显示
                                                    string strTempImgPath = SaveImgToTempPath(sourceFileName, strUseFileName);
                                                    ////在状态表T_State当中插入记录                                       
                                                    //InsertFTPUpLoadLog("华东区域预报", strItemName + "落区图", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strUseFileName, userName, strIP, strEnd, "Type");
                                                    InsertFTPUpLoadLogNew("AQI预报", "华东区域"+strItemName + "落区预报图", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strUseFileName, userName, strIP, strEnd, "Type", strTempImgPath);
                                                    intSuccessCount++;
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length * imgs.Length - intSuccessCount;

                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 13:30:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");
                    if (intSuccessCount == ftpList.Length * (imgs.Length - 1))
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "3");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length * (imgs.Length - 1))
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "4");
                        //表示发布不完全
                        return "less";
                    }
                }
            }

            return "fail";
        }

        public string UploadHazeDropZoneImgsToFTPPast(string ftpString, string fileDate, string functionName, string imgURLs, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            string[] imgs = null;
            if (imgURLs != "")
            {
                imgs = imgURLs.Split(',');
            }
            else
            {
                return "fail";
            }

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }

                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "GIF" || strFileSuffix == "gif" || strFileSuffix == "jpg")
                                {
                                    string strIP = HttpClientHelper.GetIP();
                                    string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    string strPubState = "1";
                                    string strFtpResult = "fail";
                                    try
                                    {
                                        string sourceFileName = "";
                                        string strItemName = "";
                                        if (imgs.Length > 0)
                                        {
                                            for (int m = 0; m < imgs.Length; m++)
                                            {
                                                sourceFileName = imgs[m].Split(':')[1];
                                                strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                strFtpResult = UpLoadImg(strFTPIPString, strFTPUser, strFTPPSW, sourceFileName, strFileName);
                                                strPubState = strFtpResult == "success" ? "0" : "1";
                                                string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                ////在状态表T_State当中插入记录                                       
                                                InsertFTPUpLoadLog("华东区域预报", "霾落区图", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type");
                                                intSuccessCount++;
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //finally
                                    //{
                                    //    string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    //    //在状态表T_State当中插入记录                                       
                                    //    InsertFTPUpLoadLog("落区图", functionName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                    //}
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length * imgs.Length - intSuccessCount;

                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 13:30:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");
                    if (intSuccessCount == ftpList.Length * imgs.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "3");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length * imgs.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "4");
                        //表示发布不完全
                        return "less";
                    }
                }
            }

            return "fail";
        }

        public string UploadHazeDropZoneImgsToFTPCopy(string ftpString, string fileDate, string functionName, string imgURLs, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            string[] imgs = null;
            if (imgURLs != "")
            {
                imgs = imgURLs.Split(',');
            }
            else
            {
                return "fail";
            }

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }

                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "GIF" || strFileSuffix == "gif" || strFileSuffix == "jpg")
                                {
                                    string strIP = HttpClientHelper.GetIP();
                                    string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    string strPubState = "1";
                                    string strFtpResult = "fail";
                                    try
                                    {
                                        string sourceFileName = "";
                                        string strItemName = "";
                                        if (imgs.Length > 0)
                                        {
                                            for (int m = 0; m < imgs.Length; m++)
                                            {
                                                sourceFileName = imgs[m].Split(':')[1];
                                                strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                strFtpResult = UpLoadImg(strFTPIPString, strFTPUser, strFTPPSW, sourceFileName, strFileName);
                                                strPubState = strFtpResult == "success" ? "0" : "1";
                                                string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                //将图片保存在服务器临时路径，发布日志预览显示
                                                string strTempImgPath = SaveImgToTempPath(sourceFileName, strFileName);
                                                ////在状态表T_State当中插入记录                                       
                                                //InsertFTPUpLoadLog("华东区域预报", "霾落区图", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type");
                                                InsertFTPUpLoadLogNew("霾预报", "华东区域霾落区预报图", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strTempImgPath);
                                                intSuccessCount++;
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //finally
                                    //{
                                    //    string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    //    //在状态表T_State当中插入记录                                       
                                    //    InsertFTPUpLoadLog("落区图", functionName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                    //}
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length * imgs.Length - intSuccessCount;

                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 13:30:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");
                    if (intSuccessCount == ftpList.Length * imgs.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "3");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length * imgs.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "4");
                        //表示发布不完全
                        return "less";
                    }
                }
            }

            return "fail";
        }

        public string UploadHazeDropZoneImgsToFTP(string ftpString, string fileDate, string functionName, string imgURLs, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            string[] imgs = null;
            if (imgURLs != "")
            {
                imgs = imgURLs.Split(',');
            }
            else
            {
                return "fail";
            }

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }

                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "GIF" || strFileSuffix == "gif" || strFileSuffix == "jpg")
                                {
                                    string strIP = HttpClientHelper.GetIP();
                                    string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    string strPubState = "1";
                                    string strFtpResult = "fail";
                                    try
                                    {
                                        string sourceFileName = "";
                                        string strItemName = "";
                                        if (imgs.Length > 0)
                                        {
                                            for (int m = 0; m < imgs.Length; m++)
                                            {
                                                sourceFileName = imgs[m].Split(':')[1];
                                                //strItemName = imgs[m].Split(':')[0];
                                                strItemName = "haze";
                                                StringBuilder sb = new StringBuilder(strFileName);
                                                string strUseFileName = "";
                                                string strTempFileName = sourceFileName.Substring(sourceFileName.LastIndexOf('/') + 1);
                                                string strSourceDate = strTempFileName.Split('_')[2];
                                                //起始日期增加的小时数
                                                string strIntervelHour = strTempFileName.Split('_')[3].Split('.')[0];
                                                if (sourceFileName.Contains("o3_1h") || sourceFileName.Contains("o3_8h"))
                                                {
                                                    strSourceDate = strTempFileName.Split('_')[3];
                                                    strIntervelHour = strTempFileName.Split('_')[4].Split('.')[0];
                                                }

                                                int intHour = 0;
                                                intHour = Convert.ToInt32(strIntervelHour);
                                                DateTime sourceDate = new DateTime(Convert.ToInt32(strSourceDate.Substring(0, 4)), Convert.ToInt32(strSourceDate.Substring(4, 2)), Convert.ToInt32(strSourceDate.Substring(6, 2)), Convert.ToInt32(strSourceDate.Substring(8, 2)), 0, 0);
                                                strUseFileName = sourceDate.ToString("yyyyMMdd20") + "_" + strItemName + "_" + sourceDate.AddHours(intHour).ToString("MMdd") + "." + strTempFileName.Split('.')[1].ToUpper();
                                                
                                                strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                //strFtpResult = UpLoadImg(strFTPIPString, strFTPUser, strFTPPSW, sourceFileName, strFileName);
                                                strFtpResult = UpLoadImg(strFTPIPString, strFTPUser, strFTPPSW, sourceFileName, strUseFileName);
                                                strPubState = strFtpResult == "success" ? "0" : "1";
                                                string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                //将图片保存在服务器临时路径，发布日志预览显示
                                                string strTempImgPath = SaveImgToTempPath(sourceFileName, strUseFileName);
                                                ////在状态表T_State当中插入记录                                       
                                                //InsertFTPUpLoadLog("华东区域预报", "霾落区图", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type");
                                                InsertFTPUpLoadLogNew("霾预报", "华东区域霾落区预报图", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strUseFileName, userName, strIP, strEnd, "Type", strTempImgPath);
                                                intSuccessCount++;
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //finally
                                    //{
                                    //    string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    //    //在状态表T_State当中插入记录                                       
                                    //    InsertFTPUpLoadLog("落区图", functionName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                    //}
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length * imgs.Length - intSuccessCount;

                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 13:30:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");
                    if (intSuccessCount == ftpList.Length * imgs.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "3");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length * imgs.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "4");
                        //表示发布不完全
                        return "less";
                    }
                }
            }

            return "fail";
        }

        public string UploadAirPolDropZoneImgsToFTPPast(string ftpString, string fileDate, string functionName, string imgURLs, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            string[] imgs = null;
            //if (imgURLs != "")
            //{
            //    imgs = imgURLs.Split(',');
            //}
            //else
            //{
            //    return "fail";
            //}
            if (imgURLs == "")
            {
                return "fail";

            }
            else
            {
                if (imgURLs.IndexOf(',') > 0)
                {
                    imgs = imgURLs.Split(',');
                }
                else
                {
                    imgs = new string[] { imgURLs };
                }
            }

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }

                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "GIF" || strFileSuffix == "gif" || strFileSuffix == "jpg")
                                {
                                    string strIP = HttpClientHelper.GetIP();
                                    string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    string strPubState = "1";
                                    string strFtpResult = "fail";
                                    try
                                    {
                                        string sourceFileName = "";
                                        string strItemName = "";
                                        if (imgs.Length > 0)
                                        {
                                            for (int m = 0; m < imgs.Length; m++)
                                            {

                                                sourceFileName = imgs[m].Split(':')[1];
                                                strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                strFtpResult = UpLoadImg(strFTPIPString, strFTPUser, strFTPPSW, sourceFileName, strFileName);
                                                strPubState = strFtpResult == "success" ? "0" : "1";
                                                string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                ////在状态表T_State当中插入记录                                       
                                                InsertFTPUpLoadLog("华东区域预报", "空气污染落区图", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type");
                                                intSuccessCount++;
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //finally
                                    //{
                                    //    string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    //    //在状态表T_State当中插入记录                                       
                                    //    InsertFTPUpLoadLog("落区图", functionName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                    //}
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length * imgs.Length - intSuccessCount;

                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 13:30:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");
                    if (intSuccessCount == ftpList.Length * imgs.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "3");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length * imgs.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "4");
                        //表示发布不完全
                        return "less";
                    }
                }
            }

            return "fail";
        }

        public string UploadAirPolDropZoneImgsToFTPCopy(string ftpString, string fileDate, string functionName, string imgURLs, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            string[] imgs = null;
            //if (imgURLs != "")
            //{
            //    imgs = imgURLs.Split(',');
            //}
            //else
            //{
            //    return "fail";
            //}
            if (imgURLs == "")
            {
                return "fail";

            }
            else
            {
                if (imgURLs.IndexOf(',') > 0)
                {
                    imgs = imgURLs.Split(',');
                }
                else
                {
                    imgs = new string[] { imgURLs };
                }
            }

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }

                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "GIF" || strFileSuffix == "gif" || strFileSuffix == "jpg")
                                {
                                    string strIP = HttpClientHelper.GetIP();
                                    string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    string strPubState = "1";
                                    string strFtpResult = "fail";
                                    try
                                    {
                                        string sourceFileName = "";
                                        string strItemName = "";
                                        if (imgs.Length > 0)
                                        {
                                            for (int m = 0; m < imgs.Length; m++)
                                            {

                                                sourceFileName = imgs[m].Split(':')[1];
                                                strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                strFtpResult = UpLoadImg(strFTPIPString, strFTPUser, strFTPPSW, sourceFileName, strFileName);
                                                strPubState = strFtpResult == "success" ? "0" : "1";
                                                string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                //将图片保存在服务器临时路径，发布日志预览显示
                                                string strTempImgPath = SaveImgToTempPath(sourceFileName, strFileName);
                                                ////在状态表T_State当中插入记录                                       
                                                //InsertFTPUpLoadLog("华东区域预报", "空气污染落区图", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type");
                                                InsertFTPUpLoadLogNew("空气污染气象条件", "华东区域空气污染气象条件落区预报图", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strTempImgPath);
                                                intSuccessCount++;
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //finally
                                    //{
                                    //    string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    //    //在状态表T_State当中插入记录                                       
                                    //    InsertFTPUpLoadLog("落区图", functionName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                    //}
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length * imgs.Length - intSuccessCount;

                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 13:30:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");
                    if (intSuccessCount == ftpList.Length * imgs.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "3");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length * imgs.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "4");
                        //表示发布不完全
                        return "less";
                    }
                }
            }

            return "fail";
        }

        public string UploadAirPolDropZoneImgsToFTP(string ftpString, string fileDate, string functionName, string imgURLs, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";
            string[] imgs = null;
            //if (imgURLs != "")
            //{
            //    imgs = imgURLs.Split(',');
            //}
            //else
            //{
            //    return "fail";
            //}
            if (imgURLs == "")
            {
                return "fail";

            }
            else
            {
                if (imgURLs.IndexOf(',') > 0)
                {
                    imgs = imgURLs.Split(',');
                }
                else
                {
                    imgs = new string[] { imgURLs };
                }
            }

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }

                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "GIF" || strFileSuffix == "gif" || strFileSuffix == "jpg")
                                {
                                    string strIP = HttpClientHelper.GetIP();
                                    string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    string strPubState = "1";
                                    string strFtpResult = "fail";
                                    try
                                    {
                                        string sourceFileName = "";
                                        string strItemName = "";
                                        if (imgs.Length > 0)
                                        {
                                            for (int m = 0; m < imgs.Length; m++)
                                            {
                                                sourceFileName = imgs[m].Split(':')[1];
                                                //strItemName = imgs[m].Split(':')[0];
                                                strItemName = "diffusion";
                                                StringBuilder sb = new StringBuilder(strFileName);
                                                string strUseFileName = "";
                                                string strTempFileName = sourceFileName.Substring(sourceFileName.LastIndexOf('/') + 1);
                                                string strSourceDate = strTempFileName.Split('_')[2];
                                                //起始日期增加的小时数
                                                string strIntervelHour = strTempFileName.Split('_')[3].Split('.')[0];
                                                if (sourceFileName.Contains("o3_1h") || sourceFileName.Contains("o3_8h"))
                                                {
                                                    strSourceDate = strTempFileName.Split('_')[3];
                                                    strIntervelHour = strTempFileName.Split('_')[4].Split('.')[0];
                                                }

                                                int intHour = 0;
                                                intHour = Convert.ToInt32(strIntervelHour);
                                                DateTime sourceDate = new DateTime(Convert.ToInt32(strSourceDate.Substring(0, 4)), Convert.ToInt32(strSourceDate.Substring(4, 2)), Convert.ToInt32(strSourceDate.Substring(6, 2)), Convert.ToInt32(strSourceDate.Substring(8, 2)), 0, 0);
                                                strUseFileName = sourceDate.ToString("yyyyMMdd20") + "_" + strItemName + "_" + sourceDate.AddHours(intHour).ToString("MMdd") + "." + strTempFileName.Split('.')[1].ToUpper();
                                                
                                                strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                //strFtpResult = UpLoadImg(strFTPIPString, strFTPUser, strFTPPSW, sourceFileName, strFileName);
                                                strFtpResult = UpLoadImg(strFTPIPString, strFTPUser, strFTPPSW, sourceFileName, strUseFileName);
                                                strPubState = strFtpResult == "success" ? "0" : "1";
                                                string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                                //将图片保存在服务器临时路径，发布日志预览显示
                                                string strTempImgPath = SaveImgToTempPath(sourceFileName, strUseFileName);
                                                ////在状态表T_State当中插入记录                                       
                                                //InsertFTPUpLoadLog("华东区域预报", "空气污染落区图", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type");
                                                InsertFTPUpLoadLogNew("空气污染气象条件", "华东区域空气污染气象条件落区预报图", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strUseFileName, userName, strIP, strEnd, "Type", strTempImgPath);
                                                intSuccessCount++;
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //finally
                                    //{
                                    //    string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    //    //在状态表T_State当中插入记录                                       
                                    //    InsertFTPUpLoadLog("落区图", functionName, strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                    //}
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length * imgs.Length - intSuccessCount;

                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 13:30:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 16:30:00.000");
                    if (intSuccessCount == ftpList.Length * imgs.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "3");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length * imgs.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //InsertIntoStateTable(functionName, strRcdTime, "4");
                        //表示发布不完全
                        return "less";
                    }
                }
            }

            return "fail";
        }

        //判断某个产品今日是否成功发布
        public bool IsPublished(string functionName, string forecastDate)
        {
            DateTime date = GetDatetime(forecastDate);
            string strCheckPubSQL = "select * from T_State where ReTime='" + forecastDate + "' and ModuleType='" + functionName + "'";
            DataTable dtIsPub = m_Database.GetDataTable(strCheckPubSQL);
            if (dtIsPub.Rows.Count > 0)
            {
                //返回是否已发布
                return dtIsPub.Rows[0]["State"].ToString() == "3";
            }
            return false;
        }

        public string JudgePublished(string functionName, string forecastDate)
        {
            DateTime date = GetDatetime(forecastDate);
            string strCheckPubSQL = "select * from T_State where ReTime='" + forecastDate + "' and ModuleType='" + functionName + "'";
            DataTable dtIsPub = m_Database.GetDataTable(strCheckPubSQL);
            if (dtIsPub.Rows.Count > 0)
            {
                //返回是否已发布
                if (dtIsPub.Rows[0]["State"].ToString() == "3")
                {
                    return "true";
                }
            }
            return "false";
        }

        //获取站点预报文件的文本内容并保存在服务器
        public string GetSiteForecastReportCopy()
        {
            string strTotalContent = "text";
            List<string> sitrList = new List<string>();
            string strGEtSitesSQL = "select station_co from sta_reg_set where height is not null";
            DataTable dtSites = m_Database.GetDataTable(strGEtSitesSQL);
            if (dtSites.Rows.Count > 0)
            {
                for (int i = 0; i < dtSites.Rows.Count; i++)
                {
                    sitrList.Add(dtSites.Rows[i]["station_co"].ToString());
                }
            }

            if (sitrList.Count > 0)
            {
                if (sitrList.Count < 10)
                {
                    strTotalContent = "00" + sitrList.Count.ToString() + "\n";
                }
                else if (sitrList.Count >= 10 && sitrList.Count < 100)
                {
                    strTotalContent = "0" + sitrList.Count.ToString() + "\n";
                }
                else
                {
                    strTotalContent = sitrList.Count.ToString() + "\n";
                }
                string strSingleSiteText = "";
                string strMaxForeDate = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                //string strMaxDateSQL = "select MAX(ForecastDate) from dbo.T_ForecastSite";
                //string strMaxDateSQL = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                //DataTable dtMax = m_Database.GetDataTable(strMaxDateSQL);
                //if (dtMax.Rows.Count == 0)
                //{
                //    strMaxForeDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd 20:00:00.000");

                //}
                //if (dtMax.Rows.Count == 0)
                //{
                //    strMaxForeDate = DateTime.Now.AddDays(-2).ToString("yyyy-MM-dd 20:00:00.000");

                //}
                //if (dtMax.Rows.Count > 0)
                //{
                //    strMaxForeDate = dtMax.Rows[0][0].ToString();
                //}
                for (int i = 0; i < sitrList.Count; i++)
                {
                    strSingleSiteText = GetAQIAreaReportText(sitrList[i], strMaxForeDate);
                    if (strSingleSiteText == "")
                    {
                        strMaxForeDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd 20:00:00.000");
                        strSingleSiteText = GetAQIAreaReportText(sitrList[i], strMaxForeDate);
                    }
                    if (strSingleSiteText == "")
                    {
                        strMaxForeDate = DateTime.Now.AddDays(-2).ToString("yyyy-MM-dd 20:00:00.000");
                        strSingleSiteText = GetAQIAreaReportText(sitrList[i], strMaxForeDate);
                    }
                    if (i < sitrList.Count - 1)
                    {
                        strTotalContent += strSingleSiteText + "\n";
                    }
                    else
                    {
                        strTotalContent += strSingleSiteText + "=" + "\r\nNNNN";
                    }
                }
                string strFileName = "Z_SEVP_C_BCSH_YYYYMMddhhmmss_P_MSP3_SH-MO_ENVAQFC_AIR_L88_ENC_YYYYMMDDHHMM_00000-07200.TXT";
                strFileName = strFileName.Replace("YYYYMMddhhmmss", DateTime.Now.ToString("yyyyMMddHHmmss"));
                strFileName = strFileName.Replace("YYYYMMDDHHMM", DateTime.Now.ToString("yyyyMMdd2000"));
                string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;

                //服务器上存储路径
                string strFilelPath = strBase + "AQI\\SiteReport\\" + strFileName;
                if (strTotalContent != "")
                {
                    using (FileStream fs = new FileStream(strFilelPath, FileMode.OpenOrCreate))
                    {
                        StreamWriter sw = new StreamWriter(fs);
                        sw.Write(strTotalContent);
                        sw.Close();
                    }
                    return "success" + "&" + strFileName;
                }
            }
            return "fail";
        }

        public string GetSiteForecastReportCopyNew()
        {
            string strForecastDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd 20:00:00.000");
            string strTotalContent = "text";
            strTotalContent = GetSiteForecastText(strForecastDate);
            string strFileName = "Z_SEVP_C_BCSH_YYYYMMddhhmmss_P_MSP3_SH-MO_ENVAQFC_AIR_L88_ENC_YYYYMMDDHHMM_00000-07200.TXT";
            strFileName = strFileName.Replace("YYYYMMddhhmmss", DateTime.Now.ToString("yyyyMMddHHmmss"));
            strFileName = strFileName.Replace("YYYYMMDDHHMM", DateTime.Now.ToString("yyyyMMdd2000"));
            string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;

            //if (strSingleSiteText == "")
            //{
            //    strMaxForeDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd 20:00:00.000");
            //    strSingleSiteText = GetAQIAreaReportText(sitrList[i], strMaxForeDate);
            //}
            //if (strSingleSiteText == "")
            //{
            //    strMaxForeDate = DateTime.Now.AddDays(-2).ToString("yyyy-MM-dd 20:00:00.000");
            //    strSingleSiteText = GetAQIAreaReportText(sitrList[i], strMaxForeDate);
            //}

            //服务器上存储路径
            string strFilelPath = strBase + "AQI\\SiteReport\\" + strFileName;
            if (strTotalContent != "")
            {
                using (FileStream fs = new FileStream(strFilelPath, FileMode.OpenOrCreate))
                {
                    StreamWriter sw = new StreamWriter(fs);
                    sw.Write(strTotalContent);
                    sw.Close();
                }
                return "success" + "&" + strFileName;
            }

            return "fail";
        }

        public string GetSiteForecastReport()
        {
            string strForecastDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd 20:00:00.000");
            string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
            string strSearchDate = DateTime.Now.ToString("yyyyMMdd2000");
            //判断是否找到用语发布的文件
            string siteFileUrl = ConfigurationManager.AppSettings["AQISiteReportURL"].ToString();
            if (Directory.Exists(siteFileUrl))
            {
                string[] filenames = Directory.GetFiles(siteFileUrl);
                if (filenames.Length > 0)
                {
                    for (int m = 0; m < filenames.Length; m++)
                    {
                        if (filenames[m].IndexOf(strSearchDate) > -1)
                        {
                            string strFilaFullPath = filenames[m];
                            return "success" + "&" + filenames[m].Substring(filenames[m].LastIndexOf("\\"));

                        }
                    }
                }
            }
            return "fail";
        }

        //上传站点预报文件
        public string UploadSiteReportFTPCopy(string ftpString, string fileDate, string functionName, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");

            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            string siteFileUrl = ConfigurationManager.AppSettings["AQISiteReportURL"].ToString();
            //string strSearchDate = date.ToString("yyyyMMdd2000");
            string strSearchDate = DateTime.Now.ToString("yyyyMMdd2000");
            //判断是否找到用语发布的文件
            bool blnFind = false;
            string txtContent = "";
            if (Directory.Exists(siteFileUrl))
            {
                string[] filenames = Directory.GetFiles(siteFileUrl);
                if (filenames.Length > 0)
                {
                    for (int m = 0; m < filenames.Length; m++)
                    {
                        if (filenames[m].IndexOf(strSearchDate) > -1)
                        {
                            string strFilaFullPath = filenames[m];

                            using (StreamReader sr = new StreamReader(strFilaFullPath, Encoding.Default))
                            {
                                string sLine = sr.ReadToEnd();
                                //while (true)
                                //{
                                //    string sLine = sr.ReadLine();
                                //    if (sLine == null)
                                //    {
                                //        break;
                                //    }
                                //    txtContent += sLine;
                                //}
                                txtContent += sLine;
                                sr.Close();
                            }
                            blnFind = true;
                            break;
                        }
                    }
                }
            }
            //未找到文件，返回失败
            if (blnFind == false)
            {
                return "未找到文件";
            }

            if (txtContent == "" || txtContent == null)
            {
                return "文本内容不能为空！";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {
                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];
                                //生成文件的时间
                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", DateTime.Now.ToString("yyyyMMddhhmmss"));
                                }
                                //起报时间
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", GetDatetime(fileDate).ToString("yyyyMMdd2000"));
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                if (strFileName.Contains("MMDD"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("MMDD", strDate);
                                }

                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    try
                                    {
                                        string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        string strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        string strIP = HttpClientHelper.GetIP();
                                        InsertFTPUpLoadLog("华东区域预报", "站点指导预报文件", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;
                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                    if (functionName == "UVForecast")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 15:20:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 18:20:00.000");
                        functionName = functionName + "_17";
                    }
                    else if (functionName == "OzoneForecast")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 00:00:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 04:00:00.000");
                    }
                    if (intSuccessCount == ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //表示发布不完全
                        return "less";
                    }
                }
            }
            return "fail";
        }

        public string UploadSiteReportFTP(string ftpString, string fileDate, string functionName, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");

            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            string siteFileUrl = ConfigurationManager.AppSettings["AQISiteReportURL"].ToString();
            //string strSearchDate = date.ToString("yyyyMMdd2000");
            string strSearchDate = DateTime.Now.ToString("yyyyMMdd2000");
            //判断是否找到用语发布的文件
            bool blnFind = false;
            string txtContent = "";
            string strFilaFullPath = "";
            if (Directory.Exists(siteFileUrl))
            {
                string[] filenames = Directory.GetFiles(siteFileUrl);
                if (filenames.Length > 0)
                {
                    for (int m = 0; m < filenames.Length; m++)
                    {
                        if (filenames[m].IndexOf(strSearchDate) > -1)
                        {
                            strFilaFullPath = filenames[m];

                            using (StreamReader sr = new StreamReader(strFilaFullPath, Encoding.Default))
                            {
                                string sLine = sr.ReadToEnd();
                                //while (true)
                                //{
                                //    string sLine = sr.ReadLine();
                                //    if (sLine == null)
                                //    {
                                //        break;
                                //    }
                                //    txtContent += sLine;
                                //}
                                txtContent += sLine;
                                sr.Close();
                            }
                            blnFind = true;
                            break;
                        }
                    }
                }
            }
            //未找到文件，返回失败
            if (blnFind == false)
            {
                return "未找到文件";
            }

            if (txtContent == "" || txtContent == null)
            {
                return "文本内容不能为空！";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";

            if (ftpString != "")
            {
                string[] ftpList = { ftpString };

                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {
                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                //string strFileName = ftpList[i].Split(',')[1];
                                ////生成文件的时间
                                //if (strFileName.Contains("YYYYMMddhhmmss"))
                                //{
                                //    strDate = date.ToString("yyyyMMddhhmmss");
                                //    strFileName = strFileName.Replace("YYYYMMddhhmmss", DateTime.Now.ToString("yyyyMMddhhmmss"));
                                //}
                                ////起报时间
                                //if (strFileName.Contains("YYYYMMDDHHmm"))
                                //{
                                //    strDate = date.ToString("yyyyMMddHHmm");
                                //    strFileName = strFileName.Replace("YYYYMMDDHHmm", GetDatetime(fileDate).ToString("yyyyMMdd2000"));
                                //}
                                //if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                //{
                                //    strDate = date.ToString("yyyyMMddHH");
                                //    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                //    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                //}
                                //if (strFileName.Contains("YYYYMMDD"))
                                //{
                                //    strDate = date.ToString("yyyyMMdd");
                                //    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                //}
                                //if (strFileName.Contains("YYMMDD"))
                                //{
                                //    strDate = date.ToString("yyMMdd");
                                //    strFileName = strFileName.Replace("YYMMDD", strDate);
                                //}
                                //if (strFileName.Contains("mmdd"))
                                //{
                                //    strDate = date.ToString("MMdd");
                                //    strFileName = strFileName.Replace("mmdd", strDate);
                                //}
                                //if (strFileName.Contains("MMDD"))
                                //{
                                //    strDate = date.ToString("MMdd");
                                //    strFileName = strFileName.Replace("MMDD", strDate);
                                //}
                                string strFileName = strFilaFullPath.Substring(strFilaFullPath.LastIndexOf("\\")+1);

                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {
                                    try
                                    {
                                        string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        string strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        string strIP = HttpClientHelper.GetIP();
                                        //InsertFTPUpLoadLog("华东区域预报", "站点指导预报文件", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type");
                                        InsertFTPUpLoadLogNew("华东区域预报", "站点指导预报文件", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strFilaFullPath);
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;
                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                    if (functionName == "UVForecast")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 15:20:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 18:20:00.000");
                        functionName = functionName + "_17";
                    }
                    else if (functionName == "OzoneForecast")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 00:00:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 04:00:00.000");
                    }
                    if (intSuccessCount == ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //表示发布不完全
                        return "less";
                    }
                }
            }
            return "fail";
        }

        //获取512个站点的预报文本
        public string GetSiteForecastText(string forecastDateTime)
        {
            string strTotalContent = "";
            string strGEtSitesSQL = "select * from sta_reg_set where height is not null";
            DataTable dtSites = m_Database.GetDataTable(strGEtSitesSQL);
            List<string> sitrList = new List<string>();

            string strAllSQL = "";
            if (dtSites.Rows.Count > 0)
            {
                for (int i = 0; i < dtSites.Rows.Count; i++)
                {
                    sitrList.Add(dtSites.Rows[i]["station_co"].ToString() + ";" + dtSites.Rows[i]["x"].ToString() + ";" + dtSites.Rows[i]["y"].ToString());
                    if (i < dtSites.Rows.Count - 1)
                    {
                        strAllSQL += "select * FROM dbo.T_ForecastSite WHERE ForecastDate='2016-01-17 20:00:00.000' AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('7','3','2','6','4','1') AND Site='" + dtSites.Rows[i]["station_co"].ToString() + "' AND durationID='10'  order by Interval asc" + " ";
                    }
                    else
                    {
                        strAllSQL += "select * FROM dbo.T_ForecastSite WHERE ForecastDate='2016-01-17 20:00:00.000' AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('7','3','2','6','4','1') AND Site='" + dtSites.Rows[i]["station_co"].ToString() + "' AND durationID='10'  order by Interval asc";
                    }

                }
            }
            DataSet dt = m_Database.GetDataset(strAllSQL);
            double dblX = 0;
            double dblY = 0;
            //纬度
            string strX = "";
            //经度
            string strY = "";
            string strAQI = "";
            string strAQIItem = "";
            string strContent = "";
            if (dt.Tables.Count > 0)
            {
                for (int i = 0; i < dt.Tables.Count; i++)
                {
                    dblX = Convert.ToDouble(sitrList[i].Split(';')[1]);
                    dblY = Convert.ToDouble(sitrList[i].Split(';')[2]);
                    Math.Round(dblX, 2, MidpointRounding.AwayFromZero);
                    strX = (Math.Round(dblX, 2, MidpointRounding.AwayFromZero) * 100).ToString();
                    strY = (Math.Round(dblY, 2, MidpointRounding.AwayFromZero) * 100).ToString();
                    DataTable dtAQI = GetReportTextAQIValueAndItemIDTable(forecastDateTime, sitrList[i].Split(';')[0]);
                    if (dtAQI.Rows.Count > 0)
                    {
                        strAQI = dtAQI.Rows[0]["AQI"].ToString();
                        strAQIItem = dtAQI.Rows[0]["ITEMID"].ToString();
                    }
                    strContent = GetReportText(dt.Tables[i], sitrList[i].Split(';')[0], strAQI, strAQIItem, strX, strY);
                    strTotalContent += strContent;
                }
            }
            return strTotalContent;
        }

        //上传紫外线预报文本（第二天的保存在服务器上）        
        public string UpLoadTxtFtpLatestForUVCopy(string ftpString, string fileDate, string functionName, string txtContent, string tomorrowContent, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            if (txtContent == "" || txtContent == null)
            {
                return "文本内容不能为空！";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";

            //上传第二天的文本
            UpLoadTomorrowUV(fileDate, tomorrowContent, userName);
            //ftp文本保存临时路径，用语发布日志预览
            string strFtpTempSavePath = ConfigurationManager.AppSettings["FtpUploadTxtTempPath"].ToString();
            if (ftpString != "")
            {
                string[] ftpList = { ftpString };
                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];

                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                if (strFileName.Contains("MMDD"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("MMDD", strDate);
                                }

                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {

                                    string strStart = "";
                                    string strPubState = "";
                                    string strEnd = "";
                                    string strIP = "";
                                    try
                                    {
                                        strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        strIP = HttpClientHelper.GetIP();
                                        //InsertFTPUpLoadLog("AQI预报产品", "紫外线预报", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //将发布的文本在服务器固定路径存储，用于发布日志预览
                                    if (!Directory.Exists(strFtpTempSavePath))
                                    {
                                        Directory.CreateDirectory(strFtpTempSavePath);
                                    }
                                    StringBuilder sb = new StringBuilder(txtContent);
                                    string strTempContent = sb.ToString().Replace("\n", "\r\n");
                                    using (FileStream tempFs = new FileStream(strFtpTempSavePath + strFileName, FileMode.OpenOrCreate))
                                    {
                                        StreamWriter sw = new StreamWriter(tempFs);
                                        sw.Write(strTempContent);
                                        sw.Close();
                                    }
                                    InsertFTPUpLoadLogNew("AQI预报产品", "紫外线预报", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strFtpTempSavePath + strFileName);
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;

                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                    if (functionName == "UVForecast")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 15:20:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 18:20:00.000");
                        functionName = functionName + "_05";
                    }
                    else if (functionName == "OzoneForecast")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:40:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 19:40:00.000");
                    }
                    if (intSuccessCount == ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //表示发布不完全
                        return "less";
                    }
                }
            }
            return "fail";
        }

        public string UpLoadTxtFtpLatestForUV(string ftpString, string ftpStringTom, string fileDate, string functionName, string txtContent, string tomorrowContent, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            if (txtContent == "" || txtContent == null)
            {
                return "文本内容不能为空！";
            }

            string strPubResult = "fail";
            DateTime dtNow = DateTime.Now;
            //10点之前，只发布第二天的文本文件
            if (dtNow.Hour < 10)
            {
                strPubResult = UpLoadTxtFtpLatestForUVTomorrow(ftpStringTom, fileDate, functionName, tomorrowContent, userName);
                return strPubResult;
            }
            //10点之后，正常的发布操作
            else
            {
                strPubResult = UpLoadTxtFtpLatestForUVToday(ftpString, fileDate, functionName, txtContent, tomorrowContent, userName);
                return strPubResult;
            }
        }

        //上传第二天的紫外线预报文本到服务器的路径上
        public void UpLoadTomorrowUV(string fileDate, string tomorrowContent, string userName)
        {
            DateTime date = GetDatetime(fileDate).AddDays(1);
            string strDate = date.ToString("MMdd");
            string strUVBaseURL = ConfigurationManager.AppSettings["UVTomorrowURL"].ToString();
            //string strUVBaseURL = System.Web.HttpContext.Current.Request.PhysicalApplicationPath+"AQI\\UV\\";    
            string strFileName = userName + "_" + "SHMMDD01.URP";
            if (tomorrowContent != "")
            {
                strFileName = strFileName.Replace("MMDD", strDate);
                //string strFilelPath = strUVBaseURL +strFileName;
                string strFilelPath = strUVBaseURL + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.AddDays(1).ToString("yyyyMMdd") + "\\" + strFileName;
                if (!Directory.Exists(strUVBaseURL + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.AddDays(1).ToString("yyyyMMdd")))
                {
                    Directory.CreateDirectory(strUVBaseURL + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.AddDays(1).ToString("yyyyMMdd"));
                }
                using (FileStream fs = new FileStream(strFilelPath, FileMode.OpenOrCreate))
                {
                    StreamWriter sw = new StreamWriter(fs);
                    sw.Write(tomorrowContent);
                    sw.Close();
                }
            }
        }

        //AQI分时段预报最后两列改变输入文本，只改变AQI值，不改变首要污染物
        public string ChangeAQI(string value, string itemID, string firstItem)
        {
            int AQIValue = ToAQI(value, itemID);
            AQIExtention aqiExt = new AQIExtention(AQIValue, int.Parse(itemID));
            string aqiColor = string.Format("class='{0}'", aqiExt.Color);
            return string.Format("{0}/<span {1}>{2}</span>", value, aqiColor, firstItem);
            //return string.Format("{0}/{2}", value, aqiColor, firstItem);
        }

        //判断某个产品今日是否成功保存或者发布，用语页面初始化底部按键的设置
        public string GetProductState(string functionName, string hourType)
        {
            string forecastDate = DateTime.Now.ToString("yyyy-MM-dd " + hourType + ":00.000");
            string strCheckPubSQL = "select * from T_State where ReTime='" + forecastDate + "' and ModuleType='" + functionName + "'";
            DataTable dtIsPub = m_Database.GetDataTable(strCheckPubSQL);
            if (dtIsPub.Rows.Count > 0)
            {
                switch (dtIsPub.Rows[0]["State"].ToString())
                {
                    case "0":
                        return "undone";
                    case "1":
                        return "saved";
                    case "2":
                        return "checked";
                    case "3":
                        return "published";
                    case "4":
                        return "less";
                }
            }
            return "undone";
        }

        //将产品状态改为已审核
        public string SetChecked(string functionName, string hourType)
        {
            string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd " + hourType + ":00.000");
            string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd " + hourType + ":00.000");
            //查询当天是否有记录
            string strQueryExistSQL = "select * from T_State where ModuleType='" + functionName + "' AND ReTime='" + strRcdTime + "'";
            DataTable dtQueryExist = m_Database.GetDataTable(strQueryExistSQL);

            //当天未插入状态记录
            if (dtQueryExist.Rows.Count > 0)
            {
                string strInsertStateSQL = "delete from T_State where ModuleType='" + functionName + "' AND ReTime='" + strRcdTime + "' " + "INSERT INTO T_State(ModuleType,ReTime,DeadLine,State,Type) VALUES('" + functionName + "','" + strRcdTime + "','" + strDeadLineTime + "','" + "2" + "','" + "2" + "')";
                m_Database.Execute(strInsertStateSQL);
                return "success";
            }
            return "fail";
        }

        //根据界面上的中心城区污染物和AQI，生成相应的文本
        public string GetAQIAreaReportText_24Copy(string siteID, string maxDate, string firstItem, string aqi)
        {
            string strContent = "";
            DateTime dtNow = DateTime.Now.Date;
            dtNow = dtNow.AddDays(1);
            string forecastDateTime = dtNow.ToString("yyyy-MM-dd 20:00:00");
            string strSQL = "";
            if (maxDate == "")
            {
                //strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + forecastDateTime + "' from dbo.T_ForecastSite) AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('7','3','2','6','4','1') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
                strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + forecastDateTime + "' from dbo.T_ForecastSite) AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('1','2','3','4','5','6') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
            }
            else
            {
                //strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + maxDate + "' AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('7','3','2','6','4','1') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
                strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + maxDate + "' AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('1','2','3','4','5','6') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
            }

            //string strSQL ="SELECT * FROM dbo.T_AreaResult where Site='58606'";
            DataTable dt = m_Database.GetDataTable(strSQL);
            string strAQIItemId = "0";

            switch (firstItem)
            {
                case "PM2.5":
                    strAQIItemId = "1";
                    break;
                case "PM10":
                    strAQIItemId = "2";
                    break;
                case "NO2":
                    strAQIItemId = "3";
                    break;
                case "O3-1小时":
                    strAQIItemId = "4";
                    break;
                case "O3-8小时":
                    strAQIItemId = "5";
                    break;
                case "CO":
                    strAQIItemId = "6";
                    break;
                case "SO2":
                    strAQIItemId = "7";
                    break;
                default:
                    strAQIItemId = "1";
                    break;
            }


            //查找对应站点的经纬度
            string strCordSQL = "SELECT * FROM dbo.sta_reg_set WHERE station_co='" + siteID + "'";
            DataTable dtXY = m_Database.GetDataTable(strCordSQL);
            //纬度
            string strX = "";
            //经度
            string strY = "";
            if (dtXY.Rows.Count > 0)
            {
                double dblX = Convert.ToDouble(dtXY.Rows[0]["x"].ToString());
                double dblY = Convert.ToDouble(dtXY.Rows[0]["y"].ToString());
                Math.Round(dblX, 2, MidpointRounding.AwayFromZero);
                strX = (Math.Round(dblX, 2, MidpointRounding.AwayFromZero) * 100).ToString();
                strY = (Math.Round(dblY, 2, MidpointRounding.AwayFromZero) * 100).ToString();

            }
            //根据站点编号，日期和24小时AQI预报值，生成预报文本
            strContent = GetReportText(dt, siteID, aqi, strAQIItemId, strX, strY);
            return strContent;
        }

        public string GetAQIAreaReportText_24(string siteID, string maxDate, string firstItem, string aqi)
        {
            string strContent = "";
            DateTime dtNow = DateTime.Now.Date;
            dtNow = dtNow.AddDays(1);
            string forecastDateTime = dtNow.ToString("yyyy-MM-dd 20:00:00");
            string strSQL = "";
            if (maxDate == "")
            {
                //strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + forecastDateTime + "' from dbo.T_ForecastSite) AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('7','3','2','6','4','1') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
                strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + forecastDateTime + "' from dbo.T_ForecastSite) AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('1','2','3','4','5','6') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
            }
            else
            {
                //strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + maxDate + "' AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('7','3','2','6','4','1') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
                strSQL = "SELECT * FROM dbo.T_ForecastSite WHERE ForecastDate='" + maxDate + "' AND Interval in ('27','30','33','36','39','42','45','48','54','60','66','72','78','84','90','96') AND ITEMID in('1','2','3','4','5','6') AND Site='" + siteID + "' AND durationID='10' order by Interval asc";
            }

            //string strSQL ="SELECT * FROM dbo.T_AreaResult where Site='58606'";
            DataTable dt = m_Database.GetDataTable(strSQL);
            string strAQIItemId = "0";

            switch (firstItem)
            {
                case "PM2.5":
                    strAQIItemId = "6";
                    break;
                case "PM10":
                    strAQIItemId = "3";
                    break;
                case "NO2":
                    strAQIItemId = "2";
                    break;
                case "O3-1h":
                    strAQIItemId = "5";
                    break;
                case "O3-8h":
                    strAQIItemId = "5";
                    break;
                case "CO":
                    strAQIItemId = "4";
                    break;
                case "SO2":
                    strAQIItemId = "1";
                    break;
                default:
                    strAQIItemId = "6";
                    break;
            }


            //查找对应站点的经纬度
            string strCordSQL = "SELECT * FROM dbo.sta_reg_set WHERE station_co='" + siteID + "'";
            DataTable dtXY = m_Database.GetDataTable(strCordSQL);
            //纬度
            string strX = "";
            //经度
            string strY = "";
            if (dtXY.Rows.Count > 0)
            {
                double dblX = Convert.ToDouble(dtXY.Rows[0]["x"].ToString());
                double dblY = Convert.ToDouble(dtXY.Rows[0]["y"].ToString());
                Math.Round(dblX, 2, MidpointRounding.AwayFromZero);
                strX = (Math.Round(dblX, 2, MidpointRounding.AwayFromZero) * 100).ToString();
                strY = (Math.Round(dblY, 2, MidpointRounding.AwayFromZero) * 100).ToString();

            }
            //根据站点编号，日期和24小时AQI预报值，生成预报文本
            strContent = GetReportTextChange(dt, siteID, aqi, strAQIItemId, strX, strY);

            return strContent;
        }

        //根据界面的中心城区AQI替换预报文本当中24小时一行
        public string ReplaceShanghaiReportContentCopy(string firstItem, string aqi)
        {
            List<string> sitrList = new List<string>();
            sitrList.Add("58367");
            string strTotalContent = "";
            if (sitrList.Count < 10)
            {
                strTotalContent = "00" + sitrList.Count.ToString() + "\n";
            }
            else if (sitrList.Count >= 10 && sitrList.Count < 100)
            {
                strTotalContent = "0" + sitrList.Count.ToString() + "\n";
            }
            else
            {
                strTotalContent = sitrList.Count.ToString() + "\n";
            }
            string strSingleSiteText = "";
            string strMaxForeDate = "";
            string strMaxDateSQL = "select MAX(ForecastDate) from dbo.T_ForecastSite";
            DataTable dtMax = m_Database.GetDataTable(strMaxDateSQL);
            if (dtMax.Rows.Count > 0)
            {
                strMaxForeDate = dtMax.Rows[0][0].ToString();
            }
            for (int i = 0; i < sitrList.Count; i++)
            {

                strSingleSiteText = GetAQIAreaReportText_24Copy(sitrList[i], strMaxForeDate, firstItem, aqi);
                if (i < sitrList.Count - 1)
                {
                    strTotalContent += strSingleSiteText + "\n";
                }
                else
                {
                    strTotalContent += strSingleSiteText + "=" + "\r\nNNNN";
                }
            }
            return strTotalContent;
        }

        public string ReplaceShanghaiReportContent(string firstItem, string aqi)
        {
            List<string> sitrList = new List<string>();
            sitrList.Add("58367");
            string strTotalContent = "";
            if (sitrList.Count < 10)
            {
                strTotalContent = "00" + sitrList.Count.ToString() + "\n";
            }
            else if (sitrList.Count >= 10 && sitrList.Count < 100)
            {
                strTotalContent = "0" + sitrList.Count.ToString() + "\n";
            }
            else
            {
                strTotalContent = sitrList.Count.ToString() + "\n";
            }
            string strSingleSiteText = "";
            string strMaxForeDate = "";
            string strMaxDateSQL = "select MAX(ForecastDate) from dbo.T_ForecastSite";
            DataTable dtMax = m_Database.GetDataTable(strMaxDateSQL);
            if (dtMax.Rows.Count > 0)
            {
                strMaxForeDate = dtMax.Rows[0][0].ToString();
            }
            for (int i = 0; i < sitrList.Count; i++)
            {

                strSingleSiteText = GetAQIAreaReportText_24(sitrList[i], strMaxForeDate, firstItem, aqi);
                if (i < sitrList.Count - 1)
                {
                    strTotalContent += strSingleSiteText + "\n";
                }
                else
                {
                    strTotalContent += strSingleSiteText + "=" + "\r\nNNNN";
                }
            }
            return strTotalContent;
        }

        public string CalculateAQLLevelAndReplaceCopy(string firstItem, string aqi)
        {
            string strAQLLevel = CalculateAQLLevel(aqi);
            string strReplaceReportText = ReplaceShanghaiReportContentCopy(firstItem, aqi);
            return strAQLLevel + "&" + strReplaceReportText;
        }

        public string CalculateAQLLevelAndReplace(string firstItem, string aqi)
        {
            if (firstItem != "" && firstItem != null && aqi != "" && aqi != null)
            {
                string strAQLLevel = CalculateAQLLevel(aqi);
                string strReplaceReportText = ReplaceShanghaiReportContent(firstItem, aqi);
                return strAQLLevel + "&" + strReplaceReportText;
            }
            return "";
        }

        //把word呢各部分的内容存成txt文件，供下次读取历史和PageOffice预览读取使用
        public string SaveWordContentToText(string wordPartContent, string pruductFileName)
        {
            string jsonContent = "";
            string strWordPartJson = ConfigurationManager.AppSettings["WordPartJsonFile"];
            if (wordPartContent != "")
            {
                string[] wordParts = wordPartContent.Split('&');
                if (wordParts.Length > 0)
                {
                    for (int i = 0; i < wordParts.Length; i++)
                    {
                        if (wordParts[i] != "")
                        {
                            //string strCellContent = wordParts[i].Split('=')[1];
                            //if (strCellContent.Contains(""))
                            //{
                            //    strCellContent += "=";
                            //}
                            if (i < wordParts.Length - 1)
                            {
                                jsonContent += "{\"" + wordParts[i].Split('=')[0] + "\":\"" + wordParts[i].Split('=')[1] + "\"},";
                            }
                            else
                            {
                                jsonContent += "{\"" + wordParts[i].Split('=')[0] + "\":\"" + wordParts[i].Split('=')[1] + "\"}";
                            }
                        }
                    }
                }
                jsonContent = "[" + jsonContent + "]";
                jsonContent = jsonContent.Replace("\n", "");
                string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
                string strDate = DateTime.Now.ToString("yyyy-MM-dd");
                string fileName = pruductFileName + "_" + strDate + ".txt";

                if (!Directory.Exists(strBase + strWordPartJson + pruductFileName))
                {
                    Directory.CreateDirectory(strBase + strWordPartJson + pruductFileName + "\\");
                }
                string strSavePath = strBase + strWordPartJson + pruductFileName + "\\" + fileName;
                if (File.Exists(strSavePath))
                {
                    File.Delete(strSavePath);
                }
                using (FileStream fs = new FileStream(strSavePath, FileMode.OpenOrCreate))
                {
                    StreamWriter sw = new StreamWriter(fs);
                    sw.Write(jsonContent);
                    sw.Close();
                }
                return "success";
            }
            return "fail";

        }

        //将界面（不是PageOffice控件）的编辑之后的word文档保存在服务器上，供发布读取
        public void SaveWord(string functionName, string wordPartContent)
        {
            if (wordPartContent != "")
            {
                string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
                //word文档模板文件名
                string strModelFileName = "";
                //生成word临时文件的文件名
                string strWordProductFileName = "";
                //存储某一个产品的临时文件件，与功能名称对应
                string strProductFileName = "";
                string strDate = DateTime.Now.ToString("yyyy");
                switch (functionName)
                {
                    case "PolWeatherAnalysis":
                        strModelFileName = "PolWeatherAnalysis.doc";
                        strProductFileName = "PollutionWeatherReport";
                        strWordProductFileName = "上海市污染天气过程跟踪解析专报" + strDate + "第N期.doc";
                        break;
                    default:
                        strModelFileName = "PolWeatherAnalysis.doc";
                        strWordProductFileName = "上海市污染天气过程跟踪解析专报" + strDate + "第N期.doc";
                        break;
                }
                string[] wordParts = wordPartContent.Split('&');
                if (wordParts.Length > 0)
                {
                    Document doc = new Document(strBase + "\\AQI\\PageOfficeWordModel\\" + strModelFileName);
                    DocumentBuilder docBuilder = new DocumentBuilder(doc);
                    // Use the indexer of the Bookmarks collection to obtain the desired bookmark.
                    Bookmark bookmark;
                    // Get the name and text of the bookmark.                                                              
                    for (int i = 0; i < wordParts.Length; i++)
                    {
                        bookmark = doc.Range.Bookmarks[wordParts[i].Split('=')[0]];
                        string userValue = wordParts[i].Split('=')[1];
                        if (userValue.Contains("[image]") && userValue.Contains("[/image]"))
                        {
                            userValue = userValue.Replace("[image]", "");
                            userValue = userValue.Replace("[/image]", "");
                            userValue = userValue.Replace("../../", strBase);
                            userValue = userValue.Replace("/", "\\");
                            docBuilder.MoveToBookmark(bookmark.Name);
                            // By default, the image is inserted at 100% scale.
                            docBuilder.InsertImage(userValue, 270, 400);
                        }
                        else
                        {
                            bookmark.Text = userValue;
                        }
                    }
                    doc.Save(strBase + "\\AQI\\WordProduct\\TempWord\\" + strProductFileName + "\\" + strWordProductFileName, Aspose.Words.SaveFormat.Doc);
                }
            }
        }

        //污染天气报告点击单个图片，获取图片路径并在选择界面上显示
        public string GetFutureThreeDaySingleImg(string period)
        {
            DateTime dtNow = DateTime.Now;
            string strForecastDate = dtNow.AddDays(-1).ToString("yyyy-MM-dd 20:00:00");
            string strSQL = "SELECT Period,('Product/'+Folder + '/' + Name) AS DM FROM dbo.T_LQHo WHERE ForecastDate='" + strForecastDate + "' and Type='07' and Period='" + period + "'";
            DataSet ds = m_Database.GetDataset(strSQL);
            StringBuilder sb = new StringBuilder();
            if (ds.Tables.Count > 0)
            {
                if (ds.Tables[0].Rows.Count > 0)
                {
                    for (int i = 0; i < ds.Tables[0].Rows.Count; i++)
                    {
                        sb.Append("\"" + ds.Tables[0].Rows[i][0].ToString() + "\":\"" + ds.Tables[0].Rows[i][1].ToString() + "\",");
                    }
                }
            }
            return "{" + sb.ToString().Trim(',') + "}";
        }


        //根据小时算法计算NO2的指数
        public string CalculateNO2WithHourMethod(string content)
        {
            string strCalContent = "";
            if (content != "")
            {
                string[] pairs = content.Split(',');
                for (int i = 0; i < pairs.Length; i++)
                {
                    double dblDencity = Convert.ToDouble(pairs[i].Split(':')[1]);
                    string conce = Math.Round(dblDencity / 1000.0, 3).ToString();
                    string strIndex = Lucas.AQI2012.ConvertAQI.ConvertToAQI(conce, 22, 11, 180).ToString();
                    strCalContent += pairs[i].Split(':')[0] + ":" + strIndex + ",";
                }
                strCalContent = strCalContent.Remove(strCalContent.Length - 1);
                return strCalContent;
            }
            return "";
        }


        public string SaveFutureTenDaysWordCopy(string wordTempContent, string productName)
        {
            if (wordTempContent != "")
            {
                string[] wordParts = wordTempContent.Split('&');
                string strSaveBaseUrl = ConfigurationManager.AppSettings["WordProductFilePath"];
                string strModelBaseUrl = ConfigurationManager.AppSettings["WordModelFilePath_2"];
                string strImgProductBaseUrl = ConfigurationManager.AppSettings["ImgProductBaseURL"];

                if (wordParts.Length > 0)
                {
                    //modelName为Word模板的文件名
                    string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
                    string strModelPath = strBase + strModelBaseUrl + productName + ".doc";

                    WordHelper wordHelper = new WordHelper(strModelPath);
                    Table templateTable = (Table)wordHelper.Document.GetChild(NodeType.Table, 0, true);

                    string newFileName = "";
                    string strIssueNum = "";
                    string strBookmarkName = "";
                    string strMarkValue = "";
                    for (int i = 0; i < wordParts.Length; i++)
                    {
                        strBookmarkName = wordParts[i].Split('=')[0];
                        strMarkValue = wordParts[i].Split('=')[1];
                        if (wordParts[i].Split('=')[0] == "PO_issueNum")
                        {
                            strIssueNum = wordParts[i].Split('=')[1];

                        }

                        if (wordParts[i].Split('=')[1].Contains("base64"))
                        {
                            string strImgSuffix = strMarkValue.Substring(strMarkValue.IndexOf("image/") + 6, strMarkValue.IndexOf(';') - strMarkValue.IndexOf("image/") - 6);
                            if (strImgSuffix == "jpeg")
                            {
                                strImgSuffix = "jpg";
                            }
                            strMarkValue = wordParts[i].Split('=')[1].Substring(wordParts[i].Split('=')[1].IndexOf("base64") + 7);
                            strMarkValue = strMarkValue.Replace("[/image]", "");

                            string strRandomPicName = DateTime.Now.ToString("yyyy-MM-dd") + "_" + strBookmarkName;
                            string strFullPath = strBase + "AQI\\ReplaceImgInWord\\" + strRandomPicName;
                            strMarkValue += "=";
                            MemoryStream stream = new MemoryStream(Convert.FromBase64String(strMarkValue));
                            Bitmap img = new Bitmap(stream);
                            img.Save(strFullPath + "." + strImgSuffix);
                            stream.Close();
                            wordHelper.InsertPic(wordParts[i].Split('=')[0], strFullPath + "." + strImgSuffix, 280, 380);
                        }

                        //else if (wordParts[i].Split('=')[1].Contains("../../"))
                        //{
                        //    strMarkValue = strMarkValue.Replace("[image]","");
                        //    strMarkValue = strMarkValue.Replace("[/image]", "");
                        //    strMarkValue = strMarkValue.Replace("../../", strBase );
                        //    strMarkValue = strMarkValue.Replace("/", "\\");
                        //    wordHelper.InsertPic(wordParts[i].Split('=')[0], strMarkValue, 200, 300);
                        //}
                        else if (wordParts[i].Split('=')[1].Contains("../"))
                        {
                            strMarkValue = strMarkValue.Replace("[image]", "");
                            strMarkValue = strMarkValue.Replace("[/image]", "");
                            if (wordParts[i].Split('=')[1].Contains("noImg"))
                            {
                                strMarkValue = strMarkValue.Replace("../", strBase);
                            }
                            else if (wordParts[i].Split('=')[1].Contains("../Product"))
                            {
                                strMarkValue = strMarkValue.Replace("../Product", strImgProductBaseUrl);

                            }
                            else if (wordParts[i].Split('=')[1].Contains("../Temp"))
                            {
                                strMarkValue = strMarkValue.Replace("../", strBase);

                            }
                            strMarkValue = strMarkValue.Replace("/", "\\");
                            wordHelper.InsertPic(wordParts[i].Split('=')[0], strMarkValue, 200, 300);
                        }
                        else
                        {
                            strMarkValue = wordParts[i].Split('=')[1];
                            wordHelper.Replace(wordParts[i].Split('=')[0], wordParts[i].Split('=')[1]);

                        }
                    }
                    //newFileName = productName + DateTime.Now.Year.ToString() + "_" + strIssueNum + ".doc";
                    newFileName = productName + "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".doc";
                    if (!Directory.Exists(strBase + strSaveBaseUrl + productName + "\\"))
                    {
                        Directory.CreateDirectory(strBase + strSaveBaseUrl + productName + "\\");
                    }
                    string strNewPath = strBase + strSaveBaseUrl + productName + "\\" + newFileName;
                    wordHelper.SaveAs(strNewPath, Aspose.Words.SaveFormat.Doc);

                    //json文本保存结果
                    string strTextSaveResult = SaveWordContentToText(wordTempContent, productName);
                    if (strTextSaveResult == "success")
                    {
                        return "success";
                    }
                }
            }
            return "fail";
        }

        public string SaveFutureTenDaysWord(string wordTempContent, string productName)
        {
            if (wordTempContent != "")
            {
                string[] wordParts = wordTempContent.Split('&');
                string strSaveBaseUrl = ConfigurationManager.AppSettings["WordProductFilePath"];
                string strModelBaseUrl = ConfigurationManager.AppSettings["WordModelFilePath_2"];
                string strImgProductBaseUrl = ConfigurationManager.AppSettings["ImgProductBaseURL"];

                if (wordParts.Length > 0)
                {
                    //modelName为Word模板的文件名
                    string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
                    string strModelPath = strBase + strModelBaseUrl + productName + ".doc";

                    WordHelper wordHelper = new WordHelper(strModelPath);
                    Table templateTable = (Table)wordHelper.Document.GetChild(NodeType.Table, 0, true);

                    string newFileName = "";
                    string strIssueNum = "";
                    string strBookmarkName = "";
                    string strMarkValue = "";
                    for (int i = 0; i < wordParts.Length; i++)
                    {
                        strBookmarkName = wordParts[i].Split('=')[0];
                        strMarkValue = wordParts[i].Split('=')[1];
                        if (wordParts[i].Split('=')[0] == "PO_issueNum")
                        {
                            strIssueNum = wordParts[i].Split('=')[1];
                        }

                        if (wordParts[i].Split('=')[1].Contains("base64"))
                        {
                            string strImgSuffix = strMarkValue.Substring(strMarkValue.IndexOf("image/") + 6, strMarkValue.IndexOf(';') - strMarkValue.IndexOf("image/") - 6);
                            if (strImgSuffix == "jpeg")
                            {
                                strImgSuffix = "jpg";
                            }
                            strMarkValue = wordParts[i].Split('=')[1].Substring(wordParts[i].Split('=')[1].IndexOf("base64") + 7);
                            strMarkValue = strMarkValue.Replace("[/image]", "");

                            string strRandomPicName = DateTime.Now.ToString("yyyy-MM-dd") + "_" + strBookmarkName;
                            string strFullPath = strBase + "AQI\\ReplaceImgInWord\\" + strRandomPicName;
                            strMarkValue += "=";
                            MemoryStream stream = new MemoryStream(Convert.FromBase64String(strMarkValue));
                            Bitmap img = new Bitmap(stream);
                            img.Save(strFullPath + "." + strImgSuffix);
                            stream.Close();
                            wordHelper.InsertPic(wordParts[i].Split('=')[0], strFullPath + "." + strImgSuffix, 280, 380);
                        }

                        //else if (wordParts[i].Split('=')[1].Contains("../../"))
                        //{
                        //    strMarkValue = strMarkValue.Replace("[image]","");
                        //    strMarkValue = strMarkValue.Replace("[/image]", "");
                        //    strMarkValue = strMarkValue.Replace("../../", strBase );
                        //    strMarkValue = strMarkValue.Replace("/", "\\");
                        //    wordHelper.InsertPic(wordParts[i].Split('=')[0], strMarkValue, 200, 300);
                        //}
                        else if (wordParts[i].Split('=')[1].Contains("../"))
                        {
                            strMarkValue = strMarkValue.Replace("[image]", "");
                            strMarkValue = strMarkValue.Replace("[/image]", "");
                            if (wordParts[i].Split('=')[1].Contains("noImg"))
                            {
                                strMarkValue = strMarkValue.Replace("../", strBase);
                            }
                            else if (wordParts[i].Split('=')[1].Contains("../Product"))
                            {
                                if (wordParts[i].Split('=')[1].Contains("?V="))
                                {
                                    strMarkValue = strMarkValue.Substring(0, strMarkValue.IndexOf("?V="));
                                    strMarkValue = strMarkValue.Replace("../Product", strImgProductBaseUrl);
                                }
                                else
                                {
                                    strMarkValue = strMarkValue.Replace("../Product", strImgProductBaseUrl);
                                }

                            }
                            else if (wordParts[i].Split('=')[1].Contains("../Temp"))
                            {
                                strMarkValue = strMarkValue.Replace("../", strBase);

                            }
                            strMarkValue = strMarkValue.Replace("/", "\\");
                            wordHelper.InsertPic(wordParts[i].Split('=')[0], strMarkValue, 200, 300);
                        }
                        else
                        {
                            strMarkValue = wordParts[i].Split('=')[1];
                            //wordHelper.Replace(wordParts[i].Split('=')[0], wordParts[i].Split('=')[1]);
                            wordHelper.Replace(wordParts[i].Split('=')[0], wordParts[i].Split('=')[1].TrimStart(' '));

                        }
                    }
                    //newFileName = productName + DateTime.Now.Year.ToString() + "_" + strIssueNum + ".doc";
                    newFileName = productName + "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".doc";
                    if (!Directory.Exists(strBase + strSaveBaseUrl + productName + "\\"))
                    {
                        Directory.CreateDirectory(strBase + strSaveBaseUrl + productName + "\\");
                    }
                    string strNewPath = strBase + strSaveBaseUrl + productName + "\\" + newFileName;
                    wordHelper.SaveAs(strNewPath, Aspose.Words.SaveFormat.Doc);

                    //json文本保存结果
                    string strTextSaveResult = SaveWordContentToText(wordTempContent, productName);
                    if (strTextSaveResult == "success")
                    {
                        return "success";
                    }
                }
            }
            return "fail";
        }


        public string GetFutureTenDaysDataCopy()
        {
            string strDate = DateTime.Now.ToString("yyy-MM-dd 00:00:00.000");
            string strTenDaysJson = "";
            string strTenDaysSQL = "select * from T_Weather where ForecastDate='" + strDate + "' order by  convert(int,Period) asc";
            DataTable tenTable = m_Database.GetDataTable(strTenDaysSQL);
            if (tenTable.Rows.Count < 10)
            {
                strDate = DateTime.Now.AddDays(-1).ToString("yyy-MM-dd 00:00:00.000");
                strTenDaysSQL = "select * from T_Weather where ForecastDate='" + strDate + "' order by  convert(int,Period) asc";
                tenTable = m_Database.GetDataTable(strTenDaysSQL);
            }
            if (tenTable.Rows.Count > 0)
            {
                string strOrderDate = "";
                string strPoLevel = "";
                string strDayWeather = "";
                string strNightWeather = "";
                string strWindDir = "";
                string strWindLevel = "";
                string strWea = "";
                string strWind = "";
                for (int i = 0; i < tenTable.Rows.Count; i++)
                {
                    if (i == 0)
                    {
                        //strOrderDate = DateTime.Parse(strDate).AddDays(1).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).ToString("dd日");
                        strOrderDate = DateTime.Parse(strDate).AddDays(1).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).Day.ToString()+"日";
                    }
                    else
                    {
                        if (DateTime.Parse(strDate).AddDays(1).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).Month == DateTime.Parse(strDate).AddDays(1).AddHours(Convert.ToInt32(tenTable.Rows[i - 1]["Period"].ToString())).Month)
                        {
                            strOrderDate = DateTime.Parse(strDate).AddDays(1).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).Day.ToString() + "日";
                        }
                        else
                        {
                            int intMonth = DateTime.Parse(strDate).AddDays(1).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).Month;

                            strOrderDate = intMonth.ToString() + "月" + DateTime.Parse(strDate).AddDays(1).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).Day.ToString() + "日";
                        }
                    }
                    //strOrderDate = DateTime.Parse(strDate).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).ToString("MM月dd日");

                    if (i < 7)
                    {
                        strTenDaysJson += "\"PO_DateSeven" + (i + 1).ToString() + "\":\"" + strOrderDate + "\",";
                    }

                    if (i < tenTable.Rows.Count - 1)
                    {
                        //strOrderDate = DateTime.Parse(strDate).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).ToString("MM月dd日");
                        strDayWeather = tenTable.Rows[i + 1]["DayWeather"].ToString();
                        strNightWeather = tenTable.Rows[i + 1]["NightWeather"].ToString();
                        strWindDir = tenTable.Rows[i + 1]["WindDir"].ToString();
                        strWindLevel = tenTable.Rows[i + 1]["WindClass"].ToString();

                        strWea = (strDayWeather == strNightWeather) ? strNightWeather : strDayWeather + "转" + strNightWeather;
                        strWind = strWindDir + strWindLevel;
                        strTenDaysJson += "\"PO_Date" + (i + 1).ToString() + "\":\"" + strOrderDate + "\",";
                        strTenDaysJson += "\"PO_Weather" + (i + 1).ToString() + "\":\"" + strWea + "\",";
                        strTenDaysJson += "\"PO_WindSpeed" + (i + 1).ToString() + "\":\"" + strWind + "\",";
                       
                    }
                    else
                    {
                        strTenDaysJson += "\"PO_Date" + (i + 1).ToString() + "\":\"" + strOrderDate + "\",";
                        strTenDaysJson += "\"PO_Weather" + (i + 1).ToString() + "\":\"" + "" + "\",";
                        strTenDaysJson += "\"PO_WindSpeed" + (i + 1).ToString() + "\":\"" + "" + "\",";
                    }
                }
                return "{" + strTenDaysJson.TrimEnd(',') + "}";
            }
            return "";
        }

        public string GetFutureTenDaysData()
        {
            //获取当天的内容
            string strTodaySavedContent = GetWordHistory("FutureTenDays");
            if (strTodaySavedContent == " ")
            {

                string strDate = DateTime.Now.ToString("yyy-MM-dd 00:00:00.000");
                string strTenDaysJson = "";
                string strTenDaysSQL = "select * from T_Weather where ForecastDate='" + strDate + "' order by  convert(int,Period) asc";
                DataTable tenTable = m_Database.GetDataTable(strTenDaysSQL);
                if (tenTable.Rows.Count < 10)
                {
                    strDate = DateTime.Now.AddDays(-1).ToString("yyy-MM-dd 00:00:00.000");
                    strTenDaysSQL = "select * from T_Weather where ForecastDate='" + strDate + "' order by  convert(int,Period) asc";
                    tenTable = m_Database.GetDataTable(strTenDaysSQL);
                }
                if (tenTable.Rows.Count > 0)
                {
                    string strOrderDate = "";
                    string strPoLevel = "";
                    string strDayWeather = "";
                    string strNightWeather = "";
                    string strWindDir = "";
                    string strWindLevel = "";
                    string strWea = "";
                    string strWind = "";
                    for (int i = 0; i < tenTable.Rows.Count; i++)
                    {
                        if (i == 0)
                        {
                            //strOrderDate = DateTime.Parse(strDate).AddDays(1).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).ToString("dd日");
                            strOrderDate = DateTime.Parse(strDate).AddDays(1).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).Day.ToString() + "日";
                        }
                        else
                        {
                            if (DateTime.Parse(strDate).AddDays(1).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).Month == DateTime.Parse(strDate).AddDays(1).AddHours(Convert.ToInt32(tenTable.Rows[i - 1]["Period"].ToString())).Month)
                            {
                                strOrderDate = DateTime.Parse(strDate).AddDays(1).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).Day.ToString() + "日";
                            }
                            else
                            {
                                int intMonth = DateTime.Parse(strDate).AddDays(1).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).Month;

                                strOrderDate = intMonth.ToString() + "月" + DateTime.Parse(strDate).AddDays(1).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).Day.ToString() + "日";
                            }
                        }
                        //strOrderDate = DateTime.Parse(strDate).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).ToString("MM月dd日");

                        if (i < 7)
                        {
                            strTenDaysJson += "\"PO_DateSeven" + (i + 1).ToString() + "\":\"" + strOrderDate + "\",";
                        }

                        if (i < tenTable.Rows.Count - 1)
                        {
                            //strOrderDate = DateTime.Parse(strDate).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).ToString("MM月dd日");
                            strDayWeather = tenTable.Rows[i + 1]["DayWeather"].ToString();
                            strNightWeather = tenTable.Rows[i + 1]["NightWeather"].ToString();
                            strWindDir = tenTable.Rows[i + 1]["WindDir"].ToString();
                            strWindLevel = tenTable.Rows[i + 1]["WindClass"].ToString();

                            strWea = (strDayWeather == strNightWeather) ? strNightWeather : strDayWeather + "转" + strNightWeather;
                            strWind = strWindDir + strWindLevel;
                            strTenDaysJson += "\"PO_Date" + (i + 1).ToString() + "\":\"" + strOrderDate + "\",";
                            strTenDaysJson += "\"PO_Weather" + (i + 1).ToString() + "\":\"" + strWea + "\",";
                            strTenDaysJson += "\"PO_WindSpeed" + (i + 1).ToString() + "\":\"" + strWind + "\",";

                        }
                        else
                        {
                            strTenDaysJson += "\"PO_Date" + (i + 1).ToString() + "\":\"" + strOrderDate + "\",";
                            strTenDaysJson += "\"PO_Weather" + (i + 1).ToString() + "\":\"" + "" + "\",";
                            strTenDaysJson += "\"PO_WindSpeed" + (i + 1).ToString() + "\":\"" + "" + "\",";
                        }
                    }
                    return "{" + strTenDaysJson.TrimEnd(',') + "}";
                }
            }
            return strTodaySavedContent;
        }

        public string PreviewFutureTenDaysWord(string fileName)
        {

            string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
            string strSaveBaseUrl = ConfigurationManager.AppSettings["ImgProductBaseURL"];
            string strProductPath = strSaveBaseUrl + "\\WordProduct\\FutureTenDays\\" + fileName + ".doc";
            if (File.Exists(strProductPath))
            {
                string strImgs = "";
                WordHelper wordHelper = new WordHelper(strProductPath);
                string strPreviewFileName = "FutureTenDaysPreview";
                string strStoreFullFile = strSaveBaseUrl + "\\WordPreview\\" + strPreviewFileName;
                if (!Directory.Exists(strStoreFullFile))
                {
                    Directory.CreateDirectory(strStoreFullFile);
                }
                //wordHelper.SaveAs(strStoreFullFile+"\\"+fileName+".jpg", Aspose.Words.SaveFormat.Jpeg);
                wordHelper.SaveToImage(strProductPath, strStoreFullFile + "\\" + fileName);
                if (Directory.Exists(strStoreFullFile + "\\" + fileName))
                {
                    string[] files = Directory.GetFiles(strStoreFullFile + "\\" + fileName);
                    for (int i = 0; i < files.Length; i++)
                    {
                        string strCutPath = files[i].Substring(files[i].IndexOf(strSaveBaseUrl));
                        strCutPath = strCutPath.Replace(strSaveBaseUrl, "..\\Product");
                        strCutPath = strCutPath.Replace("\\", "/");
                        strImgs += strCutPath + ",";
                    }

                }
                return strImgs.TrimEnd(',');
            }
            return "";
        }

        public string UploadWordToFTP_PageOffice(string strFTPIPString, string strSourceFileName, string strFTPUser, string strFTPPSW, string newFileName)
        {
            try
            {
                string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
                //Word文档再服务器上保存的路径
                //string strWordUrl = strBase + "AQI\\WordProduct\\" + strSourceFileName;
                string strWordUrl = strSourceFileName;
                Ftp ftp = new Ftp(strFTPIPString, strFTPUser, strFTPPSW);
                ftp.Upload(strWordUrl, newFileName);
                return newFileName;
            }
            catch { }
            return "";
        }

        //发布未来10天
        public string PublishFutureTenDaysWord(string ftpString, string functionName, string issueNum, string userName)
        {
            string strSearchDate = DateTime.Now.ToString("yyyy-MM-dd");
            string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
            string strWordProductPath = ConfigurationManager.AppSettings["WordProductFilePath"];
            string strProductPath = strBase + strWordProductPath + functionName;
            string strUpFileName = "";
            string strSourceFileName = "";
            if (Directory.Exists(strProductPath))
            {
                string[] files = Directory.GetFiles(strProductPath);
                int intFilesCount = files.Length;
                if (intFilesCount > 0)
                {
                    for (int i = 0; i < intFilesCount; i++)
                    {
                        if (files[i].Contains(strSearchDate))
                        {
                            strUpFileName = files[i];
                            strSourceFileName = files[i];

                            break;
                        }
                    }
                }
            }
            if (strUpFileName != "")
            {

                //上传成功的数目
                int intSuccessCount = 0;
                //上传成功的数目
                int intFailCount = 0;
                string strFTPIPString = null;
                string strFTPUser = null;
                string strFTPPSW = null;
                string strFtpIP = "";
                string strRcdTime = "";
                string strDeadLineTime = "";
                if (ftpString != "")
                {
                    string[] ftpList = { ftpString };

                    if (ftpString.IndexOf(';') > 0)
                    {
                        ftpList = ftpString.Split(';');
                    }

                    if (ftpList.Length > 0)
                    {
                        string strProType = "";
                        string strProName = "";
                        if (functionName == "FutureTenDays")
                        {
                            strProType = "环境专报";
                            strProName = "未来10天预报";
                        }
                        else if (functionName == "PolWeatherAnalysis")
                        {
                            strProType = "环境专报";
                            strProName = "污染天气过程跟踪解析";
                        }
                        else if (functionName == "ImportWeather")
                        {
                            strProType = "专报公报";
                            strProName = "重要天气专报";
                        }
                        else if (functionName == "WeekPolWeather")
                        {
                            strProType = "专报公报";
                            strProName = "一周污染天气展望";
                        }
                        else if (functionName == "MainCityForecast")
                        {
                            strProType = "重点城市预报";
                            strProName = "重点城市预报";
                        }
                        for (int i = 0; i < ftpList.Length; i++)
                        {
                            string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                            if (strFtpContent != "")
                            {
                                string[] ftpInfo = strFtpContent.Split(';');
                                strFTPIPString = ftpInfo[0].Split('=')[1];
                                strFTPIPString = strFTPIPString.Replace("YYYY", DateTime.Now.Year.ToString());
                                strFtpIP = strFTPIPString;
                                strFTPUser = ftpInfo[1].Split('=')[1];
                                strFTPPSW = ftpInfo[2].Split('=')[1];
                                if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                                {
                                    try
                                    {
                                        if (strFtpIP.IndexOf('/') > 0)
                                        {
                                            strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                        }

                                        //存储的文件名
                                        string strFileName = ftpList[i].Split(',')[1];
                                        strFileName = strFileName.Replace("YYYY", DateTime.Now.Year.ToString());
                                        strFileName = strFileName.Replace("N", issueNum);
                                        string strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                        string strFtpResult = UploadWordToFTP_PageOffice(strFTPIPString, strSourceFileName, strFTPUser, strFTPPSW, strFileName);
                                        string strPubState = strFtpResult == "" ? "1" : "0";
                                        string strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        //在状态表T_State当中插入记录
                                        string strIP = HttpClientHelper.GetIP();

                                        //InsertFTPUpLoadLog("AQI分区预报", "AQI分区Word文档", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");



                                        InsertFTPUpLoadLog(strProType, strProName, strStart, strEnd, strPubState, "", userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                }
                            }
                        }
                        if (intSuccessCount == ftpList.Length)
                        {
                            InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                            //InsertIntoStateTable(functionName, strRcdTime, "3");
                            //InsertIntoStateTable("AQIAreaForeFile", strRcdTime, strDeadLineTime, "3", "2");
                            //InsertIntoStateTable("AQIAreaForeFile", strRcdTime, "3");
                            //表示全部发布成功
                            return "success";
                        }
                        else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                        {
                            InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                            //InsertIntoStateTable(functionName, strRcdTime, "4");
                            //表示发布不完全
                            return "less";
                        }
                    }
                }
            }
            return "fail";
        }

        //读取word文档模板数据
        public string ReadPreviewModelModel(string productName)
        {
            string strModelJson = "";
            if (productName != "")
            {
                string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
                string strModelBaseUrl = ConfigurationManager.AppSettings["WordModelFilePath_2"];
                string strModelPath = strBase + strModelBaseUrl + productName + ".doc";
                if (File.Exists(strModelPath))
                {
                    WordHelper wordHelper = new WordHelper(strModelPath);
                    string strBookMark = wordHelper.Document.Range.Bookmarks[0].ToString();
                    if (wordHelper.Document.Range.Bookmarks.Count > 0)
                    {
                        for (int i = 0; i < wordHelper.Document.Range.Bookmarks.Count; i++)
                        {
                            strModelJson += "\"" + wordHelper.Document.Range.Bookmarks[i].Name + "\":\"" + wordHelper.Document.Range.Bookmarks[i].Text + "\",";
                        }
                        strModelJson = strModelJson.Replace("\r", "");
                        return "{" + strModelJson.TrimEnd(',') + "}";
                    }
                }
            }
            return "";
        }

        public string GetWordHistory(string productName)
        {
            string strWordPartJson = ConfigurationManager.AppSettings["WordPartJsonFile"] + productName;
            string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
            string strDate = DateTime.Now.ToString("yyyy-MM-dd");
            string fileName = productName + "_" + strDate + ".txt";
            string strSavePath = strBase + strWordPartJson + "\\" + fileName;
            string wordTempContent = "";
            if (File.Exists(strSavePath))
            {
                string strReturnJson = "";
                StreamReader reader = new StreamReader(strSavePath);
                wordTempContent = reader.ReadToEnd();
                reader.Close();

                wordTempContent = wordTempContent.Replace("[{", "");
                wordTempContent = wordTempContent.Replace("}]", "");
                wordTempContent = wordTempContent.Replace("},{", "&");
                wordTempContent = wordTempContent.Replace("\"", "");
                string[] wordParts = wordTempContent.Split('&');
                for (int i = 0; i < wordParts.Length; i++)
                {
                    strReturnJson += "\"" + wordParts[i].Split(':')[0] + "\":\"" + wordParts[i].Split(':')[1] + "\",";
                }
                return "{" + strReturnJson + "}";
            }
            return "";
        }

        //根据年份和期数查询
        public string QueryWordWithYearAndIssueCopy(string productName, string year, string issue)
        {
            if (productName != "" && year != "" && issue != "")
            {
                string strWordPartJson = ConfigurationManager.AppSettings["WordPartJsonFile"] + productName;
                string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
                string strDate = DateTime.Now.ToString("yyyy-MM-dd");
                string fileName = productName + "_" + strDate + ".txt";
                string strSavePath = strBase + strWordPartJson + "\\" + fileName;
                string strFindFileName = "";
                if (Directory.Exists(strBase + strWordPartJson + productName))
                {
                    string[] files = Directory.GetFiles(strBase + strWordPartJson + productName);
                    if (files.Length > 0)
                    {
                        for (int i = 0; i < files.Length; i++)
                        {
                            if (files[i].Contains(year) && files[i].Contains(issue))
                            {
                                strFindFileName = files[i];

                                break;
                            }
                        }
                    }
                }
                string wordTempContent = "";
                if (File.Exists(strSavePath))
                {
                    string strReturnJson = "";
                    StreamReader reader = new StreamReader(strSavePath);
                    wordTempContent = reader.ReadToEnd();
                    reader.Close();

                    wordTempContent = wordTempContent.Replace("[{", "");
                    wordTempContent = wordTempContent.Replace("}]", "");
                    wordTempContent = wordTempContent.Replace("},{", "&");
                    wordTempContent = wordTempContent.Replace("\"", "");
                    string[] wordParts = wordTempContent.Split('&');
                    for (int i = 0; i < wordParts.Length; i++)
                    {
                        strReturnJson += "\"" + wordParts[i].Split(':')[0] + "\":\"" + wordParts[i].Split(':')[1] + "\",";
                    }
                    return "{" + strReturnJson + "}";
                }
                return "";
            }
            return "";
        }

        public string QueryWordWithYearAndIssue(string productName, string searchDate)
        {
            if (searchDate != "" && productName != "")
            {
                string[] dateContent = searchDate.Split('年', '月', '日');
                string year = dateContent[0];
                string month = dateContent[1];
                string day = dateContent[2];
                if (year != "" && month != "" && day != "")
                {
                    string strWordPartJson = ConfigurationManager.AppSettings["WordPartJsonFile"];
                    string strBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
                    DateTime date = new DateTime(Convert.ToInt32(year), Convert.ToInt32(month), Convert.ToInt32(day));
                    string strDate = date.ToString("yyyy-MM-dd");
                    string fileName = productName + "_" + strDate + ".txt";
                    string strSavePath = strBase + strWordPartJson + fileName;
                    string strFindFileName = "";
                    if (Directory.Exists(strBase + strWordPartJson + productName))
                    {
                        string[] files = Directory.GetFiles(strBase + strWordPartJson + productName);
                        if (files.Length > 0)
                        {
                            for (int i = 0; i < files.Length; i++)
                            {
                                if (files[i].Contains(fileName))
                                {
                                    strFindFileName = files[i];
                                    break;
                                }
                            }
                        }
                    }
                    string wordTempContent = "";
                    if (File.Exists(strFindFileName))
                    {
                        string strReturnJson = "";
                        StreamReader reader = new StreamReader(strFindFileName);
                        wordTempContent = reader.ReadToEnd();
                        reader.Close();

                        wordTempContent = wordTempContent.Replace("[{", "");
                        wordTempContent = wordTempContent.Replace("}]", "");
                        wordTempContent = wordTempContent.Replace("},{", "&");
                        wordTempContent = wordTempContent.Replace("\"", "");
                        string[] wordParts = wordTempContent.Split('&');
                        for (int i = 0; i < wordParts.Length; i++)
                        {
                            strReturnJson += "\"" + wordParts[i].Split(':')[0] + "\":\"" + wordParts[i].Split(':')[1] + "\",";
                        }
                        return "{" + strReturnJson + "}";
                    }
                }
            }
            return "";
        }
        public void ReadFromWord()
        {
            //将更新之后的内容也保存到json文本文件里面

            //string newFileName = strProductName + "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".doc";

            //string strNewPath = strBase + strWordProFileBasePath + strProductName + "\\" + newFileName;
            //if (File.Exists(strNewPath))
            //{
            //    WordHelper wordHelper = new MMShareBLL.DAL.WordHelper(strNewPath);
            //    wordHelper.SaveAs(strNewPath, Aspose.Words.SaveFormat.Doc);
            //    string strBookMark = wordHelper.Document.Range.Bookmarks[0].ToString();
            //    if (wordHelper.Document.Range.Bookmarks.Count > 0)
            //    {
            //        string strWordContent = "";
            //        for (int i = 0; i < wordHelper.Document.Range.Bookmarks.Count; i++)
            //        {
            //            if (i < wordHelper.Document.Range.Bookmarks.Count - 1)
            //            {
            //                strWordContent += "{\"" + wordHelper.Document.Range.Bookmarks[i].Name + "\":\"" + wordHelper.Document.Range.Bookmarks[i].Text + "\"},";
            //            }
            //            else
            //            {
            //                strWordContent += "{\"" + wordHelper.Document.Range.Bookmarks[i].Name + "\":\"" + wordHelper.Document.Range.Bookmarks[i].Text + "\"}";
            //            }
            //        }
            //        strWordContent = "[" + strWordContent + "]";
            //        strWordContent = strWordContent.Replace("\n", "");
            //        MMShareBLL.DAL.AQIForecast aqiForecast = new MMShareBLL.DAL.AQIForecast();
            //        string strJsonFile = strBase + strWordJsonFileBasePath + strProductName + "" + "\\" + strProductName + "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
            //        if (File.Exists(strJsonFile))
            //        {
            //            File.Delete(strJsonFile);
            //        }
            //        aqiForecast.SaveWordContentToText(strWordContent, strJsonFile);
            //    }

            //}
        }

        public string UpdateChemistryImg(string imgFileName)
        {
            string strChemistryFtp = ConfigurationManager.AppSettings["ChemistryFtp"];

            if (strChemistryFtp != "")
            {
                string s = "integ_Shanghai_201601212000_000";
                string strAimPath = "F:/EMFCDatabase/ForecastProduct";
                string[] ftps = strChemistryFtp.Split(';');
                string strFtpIp = ftps[0];
                string strUser = ftps[1];
                string strPwd = ftps[2];
                Ftp ftp = new Ftp(strFtpIp, strUser, strPwd);
                string[] arrar = imgFileName.Split('_');
                DateTime m_Time = Convert.ToDateTime(string.Format("{0}-{1}-{2}", arrar[2].Substring(0, 4), arrar[2].Substring(4, 2), arrar[2].Substring(6, 2)));
                string city = arrar[1];
                string sourcename = (string.Format("{0}/{1}12/majorcity/integ.{2}.{3}12.png", strFtpIp, m_Time.ToString("yyyyMMdd"), city, m_Time.ToString("yyyyMMdd")));
                string aimname = (string.Format("{0}/{1}/{2}/integ_{3}_{4}2000_000.png", strAimPath, m_Time.ToString("yyyy"), m_Time.ToString("yyyyMMdd"), city, m_Time.ToString("yyyyMMdd")));

                try
                {
                    ftp.DownloadToDifferentFileName(strUser, strPwd, "ftp://" + sourcename, "", string.Format("{0}/{1}/{2}", strAimPath, m_Time.ToString("yyyy"), m_Time.ToString("yyyyMMdd")), imgFileName);
                    return aimname;
                }
                catch { }
                return "";
            }
            return "";
            //UpateInteg upateInteg = new UpateInteg(imgFileName, "X:/IntegratedAnalysis", "F:/EMFCDatabase/ForecastProduct");
            //string localpath = upateInteg.UpateData();
            //return localpath;
        }

        public string JudgeIsFilePath(string inputContent)
        {
            string pattern = @"^[a-zA-Z]:(((\\(?! )[^/:*?<>\""|\\]+)+\\?)|(\\)?)\s*$";
            Regex regex = new Regex(pattern);
            string strTempLocalPath = "";
            if (inputContent.Contains("[image]") && inputContent.Contains("[/image]"))
            {
                int intIimgStartIndex = inputContent.IndexOf("[image]") + 7;
                int intImgEndIndex = inputContent.IndexOf("[/image]");
                if (regex.IsMatch(inputContent.Substring(intIimgStartIndex, intImgEndIndex - intIimgStartIndex)))
                {
                    strTempLocalPath = inputContent.Substring(intIimgStartIndex, intImgEndIndex - intIimgStartIndex);
                }
            }
            return strTempLocalPath;
        }

        //读取昨天生成，今天上午上传的txt紫外线文本
        public string ReadTomorrowUVText()
        {
            string strUVBaseURL = ConfigurationManager.AppSettings["UVTomorrowURL"].ToString();
            string strSearchDate = DateTime.Now.ToString("MMdd");
            string strFileName = "";
            if (Directory.Exists(strUVBaseURL))
            {
                int intFileCount = Directory.GetFiles(strUVBaseURL).Length;
                string[] files = Directory.GetFiles(strUVBaseURL);
                for (int i = 0; i < intFileCount; i++)
                {
                    if (files[i].Contains(strSearchDate))
                    {
                        strFileName = files[i];
                        break;
                    }
                }

                if (strFileName != "")
                {
                    StreamReader sr = new StreamReader(strFileName, false);
                    string strContent = sr.ReadToEnd();
                    sr.Close();
                    return strContent;
                }
            }
            return "";
        }

        //获取用语发布日志预览的已发布文本内容
        public string GetTempPubLogTextContent(string filePath)
        {
            string strUsePath = "";
            if (filePath != "")
            {
                strUsePath = filePath.Replace("&", "\\");
            }
            if (File.Exists(strUsePath))
            {
                StreamReader sr = new StreamReader(strUsePath, false);
                string strContent = sr.ReadToEnd();
                sr.Close();
                return strContent;
            }
            return "";
        }

        public string ReadTomorrowUVFile()
        {
            string strUVBaseURL = ConfigurationManager.AppSettings["UVTomorrowURL"].ToString();
            string strFilePath = strUVBaseURL + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.AddDays(1).ToString("yyyyMMdd");
            if (Directory.Exists(strFilePath))
            {
                if (Directory.GetFiles(strFilePath).Length > 0)
                {
                    StreamReader sr = new StreamReader(Directory.GetFiles(strFilePath)[0], false);
                    string strContent = sr.ReadToEnd();
                    sr.Close();
                    return strContent;
                }
            }
            return "";
        }

        //上传第二天的UV文本到FTP
        public void UploadTomorrowUVFtp()
        {

        }

        //加入今天为10月11日，昨天已制作好今天要自动上传的UV文本文件（SH1011.URP），该方法对SH1011.URP进行修改
        public void UpLoadTomorrowUVAdjustToday(string fileDate, string tomorrowContent, string userName)
        {
            //DateTime date = GetDatetime(fileDate).AddDays(1);
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("MMdd");
            string strUVBaseURL = ConfigurationManager.AppSettings["UVTomorrowURL"].ToString();
            //string strUVBaseURL = System.Web.HttpContext.Current.Request.PhysicalApplicationPath+"AQI\\UV\\";    
            string strFileName = userName + "_" + "SHMMDD01.URP";
            if (tomorrowContent != "")
            {
                strFileName = strFileName.Replace("MMDD", strDate);
                //string strFilelPath = strUVBaseURL +strFileName;
                string strFilelPath = strUVBaseURL + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.ToString("yyyyMMdd") + "\\" + strFileName;
                if (!Directory.Exists(strUVBaseURL + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.ToString("yyyyMMdd")))
                {
                    Directory.CreateDirectory(strUVBaseURL + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.ToString("yyyyMMdd"));
                }
                using (FileStream fs = new FileStream(strFilelPath, FileMode.OpenOrCreate))
                {
                    StreamWriter sw = new StreamWriter(fs);
                    sw.Write(tomorrowContent);
                    sw.Close();
                }
            }
        }

        //只上传今天的UV文本
        public string UpLoadTxtFtpLatestForUVToday(string ftpString, string fileDate, string functionName, string txtContent, string tomorrowContent, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            if (txtContent == "" || txtContent == null)
            {
                return "文本内容不能为空！";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";

            //上传第二天的文本
            UpLoadTomorrowUV(fileDate, tomorrowContent, userName);
            //ftp文本保存临时路径，用语发布日志预览
            string strFtpTempSavePath = ConfigurationManager.AppSettings["FtpUploadTxtTempPath"].ToString();
            if (ftpString != "")
            {
                string[] ftpList = { ftpString };
                if (ftpString.IndexOf(';') > 0)
                {
                    ftpList = ftpString.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];

                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                if (strFileName.Contains("MMDD"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("MMDD", strDate);
                                }

                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {

                                    string strStart = "";
                                    string strPubState = "";
                                    string strEnd = "";
                                    string strIP = "";
                                    try
                                    {
                                        strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, txtContent);
                                        strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        strIP = HttpClientHelper.GetIP();
                                        //InsertFTPUpLoadLog("AQI预报产品", "紫外线预报", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //将发布的文本在服务器固定路径存储，用于发布日志预览
                                    if (!Directory.Exists(strFtpTempSavePath))
                                    {
                                        Directory.CreateDirectory(strFtpTempSavePath);
                                    }
                                    StringBuilder sb = new StringBuilder(txtContent);
                                    string strTempContent = sb.ToString().Replace("\n", "\r\n");
                                    using (FileStream tempFs = new FileStream(strFtpTempSavePath + strFileName, FileMode.OpenOrCreate))
                                    {
                                        StreamWriter sw = new StreamWriter(tempFs);
                                        sw.Write(strTempContent);
                                        sw.Close();
                                    }
                                    InsertFTPUpLoadLogNew("紫外线预报", "紫外线预报", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strFtpTempSavePath + strFileName);
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;

                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                    if (functionName == "UVForecast")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 15:20:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 18:20:00.000");
                        functionName = functionName + "_17";
                    }
                    else if (functionName == "OzoneForecast")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:40:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 19:40:00.000");
                    }
                    if (intSuccessCount == ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //表示发布不完全
                        return "less";
                    }
                }
            }
            return "fail";
        }

        public string UpLoadTxtFtpLatestForUVTomorrow(string ftpStringTom, string fileDate, string functionName, string tomorrowContent, string userName)
        {
            DateTime date = GetDatetime(fileDate);
            string strDate = date.ToString("yyyyMMdd");
            //上传成功的数目
            int intSuccessCount = 0;
            //上传成功的数目
            int intFailCount = 0;
            if (tomorrowContent == "" || tomorrowContent == null)
            {
                return "文本内容不能为空！";
            }
            string strFTPIPString = null;
            string strFTPUser = null;
            string strFTPPSW = null;
            string strFtpIP = "";

            //ftp文本保存临时路径，用语发布日志预览
            string strFtpTempSavePath = ConfigurationManager.AppSettings["FtpUploadTxtTempPath"].ToString();
            if (ftpStringTom != "")
            {
                string[] ftpList = { ftpStringTom };
                if (ftpStringTom.IndexOf(';') > 0)
                {
                    ftpList = ftpStringTom.Split(';');
                }

                if (ftpList.Length > 0)
                {
                    for (int i = 0; i < ftpList.Length; i++)
                    {

                        string strFtpContent = ConfigurationManager.AppSettings[ftpList[i].Split(',')[0]].ToString();
                        if (strFtpContent != "")
                        {
                            string[] ftpInfo = strFtpContent.Split(';');
                            strFTPIPString = ftpInfo[0].Split('=')[1];
                            strFtpIP = strFTPIPString;
                            strFTPUser = ftpInfo[1].Split('=')[1];
                            strFTPPSW = ftpInfo[2].Split('=')[1];
                            if (strFTPIPString != "" && strFTPUser != "" && strFTPPSW != null)
                            {
                                if (strFtpIP.IndexOf('/') > 0)
                                {
                                    strFtpIP = strFtpIP.Substring(0, strFtpIP.IndexOf('/'));
                                }
                                //存储的文件名
                                string strFileName = ftpList[i].Split(',')[1];

                                if (strFileName.Contains("YYYYMMddhhmmss"))
                                {
                                    strDate = date.ToString("yyyyMMddhhmmss");
                                    strFileName = strFileName.Replace("YYYYMMddhhmmss", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHHmm"))
                                {
                                    strDate = date.ToString("yyyyMMddHHmm");
                                    strFileName = strFileName.Replace("YYYYMMDDHHmm", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDDHH") || strFileName.Contains("YyYyMmDdHh"))
                                {
                                    strDate = date.ToString("yyyyMMddHH");
                                    strFileName = strFileName.Replace("YYYYMMDDHH", strDate);
                                    strFileName = strFileName.Replace("YyYyMmDdHh", strDate);
                                }
                                if (strFileName.Contains("YYYYMMDD"))
                                {
                                    strDate = date.ToString("yyyyMMdd");
                                    strFileName = strFileName.Replace("YYYYMMDD", strDate);
                                }
                                if (strFileName.Contains("YYMMDD"))
                                {
                                    strDate = date.ToString("yyMMdd");
                                    strFileName = strFileName.Replace("YYMMDD", strDate);
                                }
                                if (strFileName.Contains("mmdd"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("mmdd", strDate);
                                }
                                if (strFileName.Contains("MMDD"))
                                {
                                    strDate = date.ToString("MMdd");
                                    strFileName = strFileName.Replace("MMDD", strDate);
                                }

                                //文件后缀名
                                string strFileSuffix = strFileName.Split('.')[1];
                                if (strFileSuffix == "txt" || strFileSuffix == "TXT" || strFileSuffix == "URP")
                                {

                                    string strStart = "";
                                    string strPubState = "";
                                    string strEnd = "";
                                    string strIP = "";
                                    try
                                    {
                                        strStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string strFtpResult = UpLoadTxtToFTP(strFTPIPString, strFileName, strFTPUser, strFTPPSW, tomorrowContent);
                                        strPubState = strFtpResult == "success" ? "0" : "1";
                                        //
                                        strEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                                        //在状态表T_State当中插入记录
                                        strIP = HttpClientHelper.GetIP();
                                        //InsertFTPUpLoadLog("AQI预报产品", "紫外线预报", strStart, strEnd, strPubState, "ftp://" + strFTPIPString, userName, strIP, strEnd, "Type");
                                        intSuccessCount++;
                                    }
                                    catch (Exception e)
                                    {
                                        intFailCount++;
                                    }
                                    //将发布的文本在服务器固定路径存储，用于发布日志预览
                                    if (!Directory.Exists(strFtpTempSavePath))
                                    {
                                        Directory.CreateDirectory(strFtpTempSavePath);
                                    }
                                    StringBuilder sb = new StringBuilder(tomorrowContent);
                                    string strTempContent = sb.ToString().Replace("\n", "\r\n");
                                    using (FileStream tempFs = new FileStream(strFtpTempSavePath + strFileName, FileMode.OpenOrCreate))
                                    {
                                        StreamWriter sw = new StreamWriter(tempFs);
                                        sw.Write(strTempContent);
                                        sw.Close();
                                    }
                                    InsertFTPUpLoadLogNew("紫外线预报", "紫外线预报", strStart, strEnd, strPubState, "ftp://" + strFTPIPString + "//" + strFileName, userName, strIP, strEnd, "Type", strFtpTempSavePath + strFileName);
                                }
                            }
                        }
                    }
                    intFailCount = ftpList.Length - intSuccessCount;

                    string strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 17:00:00.000");
                    string strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 20:00:00.000");
                    if (functionName == "UVForecast")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 15:20:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 18:20:00.000");
                        functionName = functionName + "_05";
                    }
                    else if (functionName == "OzoneForecast")
                    {
                        strRcdTime = DateTime.Now.ToString("yyyy-MM-dd 16:40:00.000");
                        strDeadLineTime = DateTime.Now.ToString("yyyy-MM-dd 19:40:00.000");
                    }
                    if (intSuccessCount == ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "3", "2");
                        //表示全部发布成功
                        return "success";
                    }
                    else if (intSuccessCount > 0 && intSuccessCount < ftpList.Length)
                    {
                        InsertIntoStateTable(functionName, strRcdTime, strDeadLineTime, "4", "2");
                        //表示发布不完全
                        return "less";
                    }
                }
            }
            return "fail";
        }

        //获取用语发布日志预览的已发布图片内容
        public string GetTempPubLogImgContent(string filePath)
        {
            string strUsePath = "";
            if (filePath != "")
            {
                filePath = filePath.Replace("&", "\\");
                //string strPrefix = filePath.Substring(0, filePath.IndexOf("WebUI\\") + 5);    
                strUsePath = filePath.Substring(filePath.IndexOf("\\AQI"));
                strUsePath = ".." + strUsePath;
                return strUsePath;
            }
            return "";
        }

        //将发布的图片咋服务器的固定路径进行保存，在发布日志预览时使用
        public string SaveImgToTempPath(string sourceFileName, string useFileName)
        {
            sourceFileName = sourceFileName.TrimStart('.', '.');
            sourceFileName = sourceFileName.Replace('/', '\\');
            string strBase = ConfigurationManager.AppSettings["ImgProductBaseURL"].ToString();
            //sourceFileName = sourceFileName.Replace(@"\Product", strBase);
            sourceFileName = sourceFileName.Replace(@"\Product", strBase);

            string strSiteBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;

            string strTempImgFilePath = strSiteBase + "AQI\\TempImg";
            if (!Directory.Exists(strTempImgFilePath))
            {
                Directory.CreateDirectory(strTempImgFilePath);
            }

            if (File.Exists(sourceFileName))
            {
                if (File.Exists(strTempImgFilePath + "\\" + useFileName))
                {
                    File.Delete(strTempImgFilePath + "\\" + useFileName);
                }
                File.Copy(sourceFileName, strTempImgFilePath + "\\" + useFileName);
                return strTempImgFilePath + "\\" + useFileName;
            }
            return "";
        }

        //获取用于发布日志预览的已发布word内容,将word转为PDF
        public string GetTempPubLogWordContent(string filePath)
        {
            filePath = filePath.Replace("&", "\\");
            if (File.Exists(filePath))
            {
                string strFileName = filePath.Substring(filePath.LastIndexOf("\\") + 2);
                //string strFtpContent = ConfigurationManager.AppSettings["InfoCenterFtp"].ToString();   
                string strSiteBase = System.Web.HttpContext.Current.Request.PhysicalApplicationPath;
                string strPDFFilePath = strSiteBase + "AQI\\WordProduct\\AQIArea\\TempPDF";
                if (strFileName.Contains("doc"))
                {
                    strFileName = strFileName.Replace("doc", "pdf");
                }
                if (strFileName.Contains("docx"))
                {
                    strFileName = strFileName.Replace("docx", "pdf");
                }
                string strFilePath = strPDFFilePath + "\\" + strFileName;
                if (File.Exists(strPDFFilePath + "\\" + strFileName))
                {
                    strFilePath = strFilePath.Substring(strFilePath.IndexOf("\\AQI") + 1);
                    return "..\\" + strFilePath;
                }
                else
                {
                    if (!Directory.Exists(strPDFFilePath))
                    {
                        Directory.CreateDirectory(strPDFFilePath);
                    }
                    WordHelper wordHelper = new WordHelper(filePath);

                    wordHelper.SaveAs(strPDFFilePath + "\\" + strFileName, Aspose.Words.SaveFormat.Pdf);

                    strFilePath = strFilePath.Substring(strFilePath.IndexOf("\\AQI") + 1);
                    return "..\\" + strFilePath;
                }
            }
            return "";
        }


        //将AQI分时段预报文本和短信内容保存在服务器的固定路径上
        public string SaveAQIPeriodTextAngMsgCopy(string textContent, string msgContent)
        {
            string strTempFilePath = ConfigurationManager.AppSettings["AQIPeriodTempPath"].ToString();
            string strFilePrefix = "AQIPeriod";
            bool blnTextResult = false;
            bool blnMsgResult = false;
            if (textContent != "" && textContent != null)
            {
                try
                {
                    if (!Directory.Exists(strTempFilePath))
                    {
                        Directory.CreateDirectory(strTempFilePath);
                    }
                    using (FileStream tempFs = new FileStream(strTempFilePath + strFilePrefix + "Text_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt", FileMode.OpenOrCreate))
                    {
                        StreamWriter sw = new StreamWriter(tempFs);
                        sw.Write(textContent);
                        sw.Close();
                        blnTextResult = true;
                    }
                }
                catch { }
            }
            if (msgContent != "" && msgContent != null)
            {
                if (!Directory.Exists(strTempFilePath))
                {
                    Directory.CreateDirectory(strTempFilePath);
                }
                try
                {
                    using (FileStream tempFs = new FileStream(strTempFilePath + strFilePrefix + "Msg_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt", FileMode.OpenOrCreate))
                    {
                        StreamWriter sw = new StreamWriter(tempFs);
                        sw.Write(msgContent);
                        sw.Close();
                        blnMsgResult = true;
                    }
                }
                catch { }
            }
            if (blnTextResult == true && blnMsgResult == true)
            {
                return "success";
            }
            else
            {
                return "fail";
            }
        }

        public string SaveAQIPeriodTextAngMsg(string textContent, string msgContent)
        {
            string strTempFilePath = ConfigurationManager.AppSettings["AQIPeriodTempPath"].ToString();
            string strFilePrefix = "AQIPeriod";
            bool blnTextResult = false;
            bool blnMsgResult = false;
            if (textContent != "" && textContent != null)
            {
                try
                {
                    textContent = textContent.Replace("\n","\r\n");
                    if (!Directory.Exists(strTempFilePath))
                    {
                        Directory.CreateDirectory(strTempFilePath);
                    }
                    if (File.Exists(strTempFilePath + strFilePrefix + "Text_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt"))
                    {
                        File.Delete(strTempFilePath + strFilePrefix + "Text_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
                    }
                    using (FileStream tempFs = new FileStream(strTempFilePath + strFilePrefix + "Text_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt", FileMode.OpenOrCreate))
                    {
                        StreamWriter sw = new StreamWriter(tempFs,Encoding.Default);
                        //StreamWriter sw = new StreamWriter(tempFs, System.Text.Encoding.GetEncoding("GB2312"));
                        //Encoding ecp1252= Encoding.GetEncoding(1252);
                        //StreamWriter sw = new StreamWriter(tempFs, ecp1252);
                        sw.Write(textContent);                        
                        sw.Close();
                        blnTextResult = true;
                    }
                }
                catch { }
            }
            //保存短信文本内容
            if (msgContent != "" && msgContent != null)
            {
                if (!Directory.Exists(strTempFilePath))
                {
                    Directory.CreateDirectory(strTempFilePath);
                }
                try
                {
                    if (File.Exists(strTempFilePath + strFilePrefix + "Msg_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt"))
                    {
                        File.Delete(strTempFilePath + strFilePrefix + "Msg_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
                    }
                    using (FileStream tempFs = new FileStream(strTempFilePath + strFilePrefix + "Msg_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt", FileMode.OpenOrCreate))
                    {
                        //StreamWriter sw = new StreamWriter(tempFs);
                        StreamWriter sw = new StreamWriter(tempFs, Encoding.Default);
                        sw.Write(msgContent);
                        sw.Close();
                        blnMsgResult = true;
                    }
                }
                catch { }
            }
            if (blnTextResult == true && blnMsgResult == true)
            {
                return "success";
            }
            else
            {
                return "fail";
            }
        }

        public string GetAQIPeriodTextContent(string contentType)
        {
            string strTempFilePath = ConfigurationManager.AppSettings["AQIPeriodTempPath"].ToString();
            string strFilePrefix = "AQIPeriod";
            string strType="Text_";
            if (contentType == "text")
            {
                strType="Text_";
            }
            else if(contentType == "msg")
            {
                strType="Msg_";
            }
            string strFilePath = strTempFilePath + strFilePrefix + strType + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
            if (File.Exists(strFilePath))
            {
                //StreamReader sr = new StreamReader(strFilePath, false);
                StreamReader sr = new StreamReader(strFilePath,Encoding.Default);
                string strContent = sr.ReadToEnd();
                sr.Close();
                return strContent;
            }
            return "";
        }

        public string SetFutureTenDaysDate()
        {
            string strDate = DateTime.Now.ToString("yyy-MM-dd 00:00:00.000");
            string strTenDaysJson = "";

                string strOrderDate = "";
                for (int i = 0; i < 10; i++)
                {
                    if (i == 0)
                    {
                        //strOrderDate = DateTime.Parse(strDate).AddDays(1).AddHours(24*i).ToString("dd日");
                        strOrderDate = DateTime.Parse(strDate).AddDays(1).AddHours(24 * i).Day.ToString() + "日";
                    }
                    else
                    {
                        if (DateTime.Parse(strDate).AddDays(1).AddHours(24 * i).Month == DateTime.Parse(strDate).AddDays(1).AddHours(24 * (i-1)).Month)
                        {
                            //strOrderDate = DateTime.Parse(strDate).AddDays(1).AddHours(24 * i).ToString("dd日");
                            strOrderDate = DateTime.Parse(strDate).AddDays(1).AddHours(24 * i).Day.ToString()+"日";
                        }
                        else
                        {
                            int intMonth = DateTime.Parse(strDate).AddDays(1).AddHours(24 * i).Month;                            
                            strOrderDate = intMonth.ToString() + "月" + DateTime.Parse(strDate).AddDays(1).AddHours(24 * i).Day.ToString()+"日";
                           
                        }
                    }
                    //strOrderDate = DateTime.Parse(strDate).AddHours(Convert.ToInt32(tenTable.Rows[i]["Period"].ToString())).ToString("MM月dd日");

                    if (i < 7)
                    {
                        strTenDaysJson += "\"PO_DateSeven" + (i + 1).ToString() + "\":\"" + strOrderDate + "\",";
                    }

                    if (i < 9)
                    {
                        strTenDaysJson += "\"PO_Date" + (i + 1).ToString() + "\":\"" + strOrderDate + "\",";                      
                    }
                    else
                    {
                        strTenDaysJson += "\"PO_Date" + (i + 1).ToString() + "\":\"" + strOrderDate + "\",";                        
                    }
                }
                return "{" + strTenDaysJson.TrimEnd(',') + "}";
        }

        //读取重要通知数据表
        public string GetImportantNoticeDataCopy(string start, string limit)
        {
            int intStart = Convert.ToInt32(start);
            int intLimit = Convert.ToInt32(limit);
            string strJson = "";
            //存储所有发布记录的集合
            List<string> totalList = new List<string>();

            string strSQL = "SELECT * FROM T_ImNotice order by ReTime desc";
            DataTable dt = m_Database.GetDataTable(strSQL);
            StringBuilder sb = null;
            if (dt.Rows.Count > 0)
            {
                if (dt.Rows.Count > 0)
                {
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        sb = new StringBuilder();
                        string strIsDis = "未停用";
                        if (dt.Rows[i]["IsDisable"].ToString() == "1")
                        {
                            strIsDis = "已停用";
                        }
                        sb.Append("{\"" + "ID" + "\":\"" + dt.Rows[i]["ID"].ToString() + "\",\""
                            + "Content" + "\":\"" + dt.Rows[i]["Content"].ToString().Replace("\r\n","") + "\",\""
                            + "Type" + "\":\"" + dt.Rows[i]["Type"].ToString() + "\",\""
                            + "Period" + "\":\"" + dt.Rows[i]["Period"].ToString() + "\",\""
                            + "StartTime" + "\":\"" + "" + "\",\""
                            + "EndTime" + "\":\"" + "" + "\",\""
                            + "IsDisable" + "\":\"" + strIsDis + "\",\""
                            + "AllPara" + "\":\"" + dt.Rows[i]["ID"].ToString() + "&" + dt.Rows[i]["ReTime"].ToString() + "&" + dt.Rows[i]["Type"].ToString() + "&" + dt.Rows[i]["Period"].ToString() + "&" + dt.Rows[i]["IsDisable"].ToString() + "&" + dt.Rows[i]["Content"].ToString()
                            //+ "IsDisable" + "\":\"" + dt.Rows[i]["IsDisable"].ToString() + "\",\""
                           
                              //+ "FileTempPath" + "\":\"" + dt.Rows[i]["FileTempPath"].ToString().Replace("\\", "&")
                            + "\"}");
                        totalList.Add(sb.ToString());
                    }
                    //string strLST = DateTime.Parse(dt.Rows[dt.Rows.Count - 1]["LST"].ToString()).ToString("yyyy年MM月dd日");
                    //sb.Append("{\"LST\":\"" + strLST + "\",\"O3\":\"" + dt.Rows[dt.Rows.Count - 1]["O3"].ToString() + "\",\"O38\":\"" + dt.Rows[dt.Rows.Count - 1]["O38"].ToString() + "\",\"O3Period\":\"" + dt.Rows[dt.Rows.Count - 1]["O3Period"].ToString() + "\",\"O38Period\":\"" + dt.Rows[dt.Rows.Count - 1]["O38Period"].ToString() + "\"},");
                }

                StringBuilder showData = new StringBuilder();
                int end = intStart + intLimit;
                int intUseEnd = end < totalList.Count ? end : totalList.Count;
                for (int i = intStart; i < intUseEnd; i++)
                {
                    showData.Append(totalList[i]);
                    if (i != end)
                    {
                        showData.Append(",");
                    }
                }

                //for (int i = 0; i < totalList.Count; i++)
                //{
                //    showData.Append(totalList[i]);
                //    if (i != totalList.Count-1)
                //    {
                //        showData.Append(",");
                //    }
                //}

                strJson = "{" +
           "\"metaData\":{" +
            " \"totalProperty\":\"results\"," +
             "\"root\":\"rows\"," +
             "\"id\":\"id\"," +
             "\"fields\":[" +
               "{\"name\":\"ID\",\"mapping\":\"ID\"}," +
               "{\"name\":\"Content\",\"mapping\":\"Content\"}," +
               "{\"name\":\"Type\",\"mapping\":\"Type\"}," +
               "{\"name\":\"Period\",\"mapping\":\"Period\"}," +
               "{\"name\":\"StartTime\",\"mapping\":\"StartTime\"}," +
               "{\"name\":\"EndTime\",\"mapping\":\"EndTime\"}," +
               "{\"name\":\"IsDisable\",\"mapping\":\"IsDisable\"}," +
              "{\"name\":\"AllPara\",\"mapping\":\"AllPara\"}" +
             "]" +
           "}," +
           "\"results\":\"" + totalList.Count + "\",\"" +
           "rows\":[" +
            showData.ToString().TrimEnd(',') +
           "]" +
         "}";

            }
            return strJson;
        }

        public string GetImportantNoticeData(string start, string limit)
        {
            int intStart = Convert.ToInt32(start);
            int intLimit = Convert.ToInt32(limit);
            string strJson = "";
            //存储所有发布记录的集合
            List<string> totalList = new List<string>();

            string strSQL = "SELECT * FROM T_ImNotice order by ReTime desc";
            //DataTable dt = m_Database.GetDataTable(strSQL);
            DataTable dt = m_DatabaseJX.GetDataTable(strSQL);
            StringBuilder sb = null;
            if (dt.Rows.Count > 0)
            {
                if (dt.Rows.Count > 0)
                {
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        sb = new StringBuilder();
                        string strIsDis = "未停用";
                        if (dt.Rows[i]["IsDisable"].ToString() == "1")
                        {
                            strIsDis = "已停用";
                        }
                        int intPeriod = 0;
                        intPeriod = Convert.ToInt32(dt.Rows[i]["Period"].ToString());
                        string strPeriod = "";
                        string strStartTime = "";
                        string strEndTime = "";
                        
                        if (intPeriod > 0)
                        {
                            int intDayCount = 0;
                            if (dt.Rows[i]["ReTime"].ToString() != "" && dt.Rows[i]["ReTime"].ToString() != null)
                            {
                                strStartTime = dt.Rows[i]["ReTime"].ToString();
                                if (dt.Rows[i]["TimeType"].ToString() == "天")
                                {
                                    intDayCount = Convert.ToInt32(dt.Rows[i]["Period"].ToString());
                                    strEndTime = Convert.ToDateTime(dt.Rows[i]["ReTime"].ToString()).AddDays(intDayCount).ToString();
                                }
                                else if (dt.Rows[i]["TimeType"].ToString() == "周")
                                {
                                    intDayCount = 7 * Convert.ToInt32(dt.Rows[i]["Period"].ToString());
                                    strEndTime = Convert.ToDateTime(dt.Rows[i]["ReTime"].ToString()).AddDays(intDayCount).ToString();
                                }
                                else if (dt.Rows[i]["TimeType"].ToString() == "月")
                                {
                                    intDayCount = Convert.ToInt32(dt.Rows[i]["Period"].ToString());
                                    strEndTime = Convert.ToDateTime(dt.Rows[i]["ReTime"].ToString()).AddMonths(intDayCount).ToString();
                                }
                            }
                            
                            strPeriod = "";
                            
                        }
                        else
                        {
                            if (dt.Rows[i]["ReTime"].ToString() != "" && dt.Rows[i]["ReTime"].ToString() != null)
                            {
                                strStartTime = dt.Rows[i]["ReTime"].ToString();                                
                            }
                            strPeriod = "长期";
                            strEndTime = "";
                        }
                        sb.Append("{\"" + "ID" + "\":\"" + dt.Rows[i]["ID"].ToString() + "\",\""
                            + "Content" + "\":\"" + dt.Rows[i]["Content"].ToString().Replace("\r\n", "") + "\",\""
                            + "Type" + "\":\"" + dt.Rows[i]["Type"].ToString() + "\",\""
                            + "Period" + "\":\"" + strPeriod + "\",\""
                            + "StartTime" + "\":\"" + strStartTime + "\",\""
                            + "EndTime" + "\":\"" + strEndTime + "\",\""
                            + "IsDisable" + "\":\"" + strIsDis + "\",\""
                            + "AllPara" + "\":\"" + dt.Rows[i]["ID"].ToString() + "&" + dt.Rows[i]["ReTime"].ToString() + "&" + dt.Rows[i]["Type"].ToString() + "&" + dt.Rows[i]["Period"].ToString() + "&" + dt.Rows[i]["IsDisable"].ToString() + "&" + dt.Rows[i]["Content"].ToString()
                            //+ "IsDisable" + "\":\"" + dt.Rows[i]["IsDisable"].ToString() + "\",\""

                              //+ "FileTempPath" + "\":\"" + dt.Rows[i]["FileTempPath"].ToString().Replace("\\", "&")
                            + "\"}");
                        totalList.Add(sb.ToString());
                    }
                    //string strLST = DateTime.Parse(dt.Rows[dt.Rows.Count - 1]["LST"].ToString()).ToString("yyyy年MM月dd日");
                    //sb.Append("{\"LST\":\"" + strLST + "\",\"O3\":\"" + dt.Rows[dt.Rows.Count - 1]["O3"].ToString() + "\",\"O38\":\"" + dt.Rows[dt.Rows.Count - 1]["O38"].ToString() + "\",\"O3Period\":\"" + dt.Rows[dt.Rows.Count - 1]["O3Period"].ToString() + "\",\"O38Period\":\"" + dt.Rows[dt.Rows.Count - 1]["O38Period"].ToString() + "\"},");
                }

                StringBuilder showData = new StringBuilder();
                int end = intStart + intLimit;
                int intUseEnd = end < totalList.Count ? end : totalList.Count;
                for (int i = intStart; i < intUseEnd; i++)
                {
                    showData.Append(totalList[i]);
                    if (i != end)
                    {
                        showData.Append(",");
                    }
                }

                //for (int i = 0; i < totalList.Count; i++)
                //{
                //    showData.Append(totalList[i]);
                //    if (i != totalList.Count-1)
                //    {
                //        showData.Append(",");
                //    }
                //}

                strJson = "{" +
           "\"metaData\":{" +
            " \"totalProperty\":\"results\"," +
             "\"root\":\"rows\"," +
             "\"id\":\"id\"," +
             "\"fields\":[" +
               "{\"name\":\"ID\",\"mapping\":\"ID\"}," +
               "{\"name\":\"Content\",\"mapping\":\"Content\"}," +
               "{\"name\":\"Type\",\"mapping\":\"Type\"}," +
               "{\"name\":\"Period\",\"mapping\":\"Period\"}," +
               "{\"name\":\"StartTime\",\"mapping\":\"StartTime\"}," +
               "{\"name\":\"EndTime\",\"mapping\":\"EndTime\"}," +
               "{\"name\":\"IsDisable\",\"mapping\":\"IsDisable\"}," +
              "{\"name\":\"AllPara\",\"mapping\":\"AllPara\"}" +
             "]" +
           "}," +
           "\"results\":\"" + totalList.Count + "\",\"" +
           "rows\":[" +
            showData.ToString().TrimEnd(',') +
           "]" +
         "}";

            }
            return strJson;
        }

        //获取用语在预报工作区首页显示的重要通知信息
        public string GetImportantNoticeDataForHomePage(string top)
        {
            int intTotalCpunt = Convert.ToInt32(top);
            string strJson = "";
            //存储所有发布记录的集合
            List<string> totalList = new List<string>();

            string strSQL = "SELECT * FROM T_ImNotice where IsDisable='0' order by ReTime desc";
            DataTable dt = m_Database.GetDataTable(strSQL);
            StringBuilder sb = null;
            if (dt.Rows.Count > 0)
            {
                if (dt.Rows.Count > 0)
                {
                    intTotalCpunt = intTotalCpunt > dt.Rows.Count ? dt.Rows.Count : intTotalCpunt;
                    for (int i = 0; i < intTotalCpunt; i++)
                    {
                        sb = new StringBuilder();
                        string strIsDis = "未停用";
                        if (dt.Rows[i]["IsDisable"].ToString() == "1")
                        {
                            strIsDis = "已停用";
                        }
                        int intPeriod = 0;
                        intPeriod = Convert.ToInt32(dt.Rows[i]["Period"].ToString());
                        string strPeriod = "";
                        string strStartTime = "";
                        string strEndTime = "";

                        if (intPeriod > 0)
                        {
                            int intDayCount = 0;
                            if (dt.Rows[i]["ReTime"].ToString() != "" && dt.Rows[i]["ReTime"].ToString() != null)
                            {
                                strStartTime = dt.Rows[i]["ReTime"].ToString();
                                if (dt.Rows[i]["TimeType"].ToString() == "天")
                                {
                                    intDayCount = Convert.ToInt32(dt.Rows[i]["Period"].ToString());
                                    strEndTime = Convert.ToDateTime(dt.Rows[i]["ReTime"].ToString()).AddDays(intDayCount).ToString();
                                }
                                else if (dt.Rows[i]["TimeType"].ToString() == "周")
                                {
                                    intDayCount = 7 * Convert.ToInt32(dt.Rows[i]["Period"].ToString());
                                    strEndTime = Convert.ToDateTime(dt.Rows[i]["ReTime"].ToString()).AddDays(intDayCount).ToString();
                                }
                                else if (dt.Rows[i]["TimeType"].ToString() == "月")
                                {
                                    intDayCount = Convert.ToInt32(dt.Rows[i]["Period"].ToString());
                                    strEndTime = Convert.ToDateTime(dt.Rows[i]["ReTime"].ToString()).AddMonths(intDayCount).ToString();
                                }
                            }

                            strPeriod = "";

                        }
                        else
                        {
                            if (dt.Rows[i]["ReTime"].ToString() != "" && dt.Rows[i]["ReTime"].ToString() != null)
                            {
                                strStartTime = dt.Rows[i]["ReTime"].ToString();
                            }
                            strPeriod = "长期";
                            strEndTime = "";
                        }
                        sb.Append("{\"" + "ID" + "\":\"" + dt.Rows[i]["ID"].ToString() + "\",\""
                            + "Content" + "\":\"" + dt.Rows[i]["Content"].ToString().Replace("\r\n", "") + "\",\""
                            + "Type" + "\":\"" + dt.Rows[i]["Type"].ToString() + "\",\""
                            + "Period" + "\":\"" + strPeriod + "\",\""
                            + "StartTime" + "\":\"" + strStartTime + "\",\""
                            + "EndTime" + "\":\"" + strEndTime + "\",\""
                            + "IsDisable" + "\":\"" + strIsDis 
                            + "\"}");
                        totalList.Add(sb.ToString());
                    }
                    //string strLST = DateTime.Parse(dt.Rows[dt.Rows.Count - 1]["LST"].ToString()).ToString("yyyy年MM月dd日");
                    //sb.Append("{\"LST\":\"" + strLST + "\",\"O3\":\"" + dt.Rows[dt.Rows.Count - 1]["O3"].ToString() + "\",\"O38\":\"" + dt.Rows[dt.Rows.Count - 1]["O38"].ToString() + "\",\"O3Period\":\"" + dt.Rows[dt.Rows.Count - 1]["O3Period"].ToString() + "\",\"O38Period\":\"" + dt.Rows[dt.Rows.Count - 1]["O38Period"].ToString() + "\"},");
                }

                StringBuilder showData = new StringBuilder();
              
                for (int i = 0; i < intTotalCpunt; i++)
                {
                    showData.Append(totalList[i]);
                    if (i != intTotalCpunt-1)
                    {
                        showData.Append(",");
                    }
                }

                return "["+showData.ToString()+"]";

            }
            return "";
        }

        //在界面上编辑，更新一条重要通知的记录
        public string RefreshImportantNoticeCopy(string id,string type,string period,string isDisable,string content,string user)
        {
            try
            {
                string strUpdateSQL = string.Format("UPDATE T_ImNotice SET Type = '{0}', Period = '{1}',IsDisable='{2}',[Content]='{3}',[User]='{4}' WHERE ID = '" + id + "'", type, period, isDisable, content, user);
                m_Database.Execute(strUpdateSQL);
                return "success";
            }
            catch 
            { }
            return "";
        }

        public string RefreshImportantNotice(string id, string type, string period,string timeType, string isDisable, string content, string user)
        {
            try
            {
                string strUpdateSQL = "";
                strUpdateSQL = string.Format("UPDATE T_ImNotice SET ReTime='" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "',Type = '{0}', Period = '{1}',TimeType='{5}',IsDisable='{2}',[Content]='{3}',[User]='{4}' WHERE ID = '" + id + "'", type, period, isDisable, content, user, timeType);               
                //m_Database.Execute(strUpdateSQL);
                m_DatabaseJX.Execute(strUpdateSQL);
                return "success";
            }
            catch
            { }
            return "";
        }

        public string AddImportantNoticeCopy(string type, string period, string isDisable, string content, string user)
        {
            try
            {
                string strUpdateSQL = string.Format("INSERT INTO T_ImNotice (Type,Period ,IsDisable,[Content],[User]) SELECT '{0}','{1}','{2}','{3}','{4}'", type, period, isDisable, content, user);
                m_Database.Execute(strUpdateSQL);
                return "success";
            }
            catch
            { }
            return "";
        }

        public string AddImportantNotice(string type, string period, string timeType, string isDisable, string content, string user)
        {
            try
            {
                string strUpdateSQL = string.Format("INSERT INTO T_ImNotice (ReTime,Type,Period,TimeType,IsDisable,[Content],[User]) SELECT '{0}','{1}','{2}','{3}','{4}','{5}','{6}'", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), type, period,timeType,isDisable, content, user);
                //m_Database.Execute(strUpdateSQL);
                m_DatabaseJX.Execute(strUpdateSQL);
                return "success";
            }
            catch
            { }
            return "";
        }

        public string DeleteImportantNotice(string ids)
        {
            try
            {
                if (ids != "" && ids != null)
                {
                    string[] idList = ids.Split(',');
                    string strIdRange = ""; 
                    for (int i = 0; i < idList.Length; i++)
                    {
                        if (i < idList.Length - 1)
                        {
                            strIdRange += "'" + idList[i] + "',";
                        }
                        else
                        {
                            strIdRange += "'" + idList[i]+"'";
                        }
                    }
                    
                    string strUpdateSQL = string.Format("delete from T_ImNotice where ID in("+strIdRange+ ")");
                    //m_Database.Execute(strUpdateSQL);
                    m_DatabaseJX.Execute(strUpdateSQL);
                    return "success";
                }
            }
            catch
            { }
            return "";
        }

        //判断当天AQI分时段短信是否已发送
        public string JudgeAQIPeriodMessage()
        {
            string strAQIPeriodMsmSQL = "select * from T_ProductLog where ProductType='AQI分时段预报短信模板' and ProductName='短信' and DATEDIFF(day,StartTime,getDate())=0 order by StartTime desc";
            DataTable dt = m_Database.GetDataTable(strAQIPeriodMsmSQL);
            if (dt.Rows.Count > 0)
            {
                //当天短信已发布
                if (dt.Rows[0]["State"].ToString() == "0")
                {
                    return "Pub";
                }
                else
                {
                    return "unPub";
                }
            }
            else
            {
                return "unPub";
            }           
        }

        //计算字符串的MD5值
        private string GetMd5Hash(string input)
        {
            if (input == null)
            {
                return null;
            }

            MD5 md5Hash = MD5.Create();

            // 将输入字符串转换为字节数组并计算哈希数据  
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // 创建一个 Stringbuilder 来收集字节并创建字符串  
            StringBuilder sBuilder = new StringBuilder();

            // 循环遍历哈希数据的每一个字节并格式化为十六进制字符串  
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // 返回十六进制字符串  
            return sBuilder.ToString();
        }  

    }
}
