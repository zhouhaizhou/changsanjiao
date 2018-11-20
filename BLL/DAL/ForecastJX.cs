using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Collections;
using System.Configuration;
using System.Net;

using System.Data;
using System.Data.SqlClient;
using Readearth.Data;
using Readearth.Data.Entity;
using MMShareBLL.Model;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
namespace MMShareBLL.DAL
{

    public class ForecastJX
    {
        Database m_Database;
        Database m_DatabaseJX;
        string m_ID;

        public ForecastJX()
        {
            m_Database = new Database();
            m_DatabaseJX = new Database("JXDBCONFIG");
        }

        public ForecastJX(Database db)
        {
            m_Database = db;
        }
        public DataTable  GetLeftPanel(string node)
        {
            string strSQL = "SELECT T_ImageProduct1.ENTITYNAME,ALIGN,PERIOD,AliasName,T_ENTITY.HINT,CLASS,cssName,MenuName,flag  FROM T_ImageProduct1 LEFT OUTER JOIN T_ENTITY ON T_ENTITY.ENTITYNAME = T_ImageProduct1.ENTITYNAME WHERE MODULENAME = '" + node + "' ORDER BY ORDERID,CLASS,MenuName";
            return m_Database.GetDataTable(strSQL);
        }

        public DataTable GetLeftPanel_JXCopy(string node)
        {
            string strSQL = "SELECT T_ImageProduct1.ENTITYNAME,ALIGN,PERIOD,AliasName,T_ENTITY.HINT,CLASS,cssName,MenuName,flag  FROM T_ImageProduct1 LEFT OUTER JOIN T_ENTITY ON T_ENTITY.ENTITYNAME = T_ImageProduct1.ENTITYNAME WHERE MODULENAME = '" + node + "' ORDER BY ORDERID,CLASS,MenuName";
            return m_DatabaseJX.GetDataTable(strSQL);
        }

        public DataTable GetLeftPanel_JX(string node)
        {
            //string strSQL = "SELECT T_ImageProduct1.ENTITYNAME,ALIGN,PERIOD,AliasName,T_ENTITY.HINT,CLASS,cssName,MenuName,flag  FROM T_ImageProduct1 LEFT OUTER JOIN T_ENTITY ON T_ENTITY.ENTITYNAME = T_ImageProduct1.ENTITYNAME WHERE MODULENAME = '" + node + "' ORDER BY ORDERID,CLASS,MenuName";
            string strSQL = "SELECT T_ImageProduct1.ENTITYNAME,ALIGN,PERIOD,AliasName,T_ENTITY.HINT,CLASS,cssName,MenuName,flag,T_UserAccess.IsAccess FROM T_ImageProduct1 LEFT OUTER JOIN T_ENTITY ON T_ENTITY.ENTITYNAME = T_ImageProduct1.ENTITYNAME LEFT OUTER JOIN T_UserAccess ON T_UserAccess.ENTITYNAME = T_ImageProduct1.ENTITYNAME WHERE MODULENAME = '" + node + "' AND " + " T_UserAccess.IsAccess='0'  ORDER BY ORDERID,CLASS,MenuName";
            return m_DatabaseJX.GetDataTable(strSQL);
        }

        public DataTable GetLeftPanel_JXSingle(string node)
        {
            //string strSQL = "SELECT T_ImageProduct1.ENTITYNAME,ALIGN,PERIOD,AliasName,T_ENTITY.HINT,CLASS,cssName,MenuName,flag  FROM T_ImageProduct1 LEFT OUTER JOIN T_ENTITY ON T_ENTITY.ENTITYNAME = T_ImageProduct1.ENTITYNAME WHERE MODULENAME = '" + node + "' AND "+ ""+" ORDER BY ORDERID,CLASS,MenuName";
            //string strSQL = "SELECT T_ImageProduct1.ENTITYNAME,ALIGN,PERIOD,AliasName,T_ENTITY.HINT,CLASS,cssName,MenuName,flag,T_UserAccess.IsAccess FROM T_ImageProduct1 LEFT OUTER JOIN T_ENTITY ON T_ENTITY.ENTITYNAME = T_ImageProduct1.ENTITYNAME LEFT OUTER JOIN T_UserAccess ON T_UserAccess.ENTITYNAME = T_ImageProduct1.ENTITYNAME WHERE MODULENAME = '" + node + "' AND " + " T_UserAccess.IsAccess='0'  ORDER BY ORDERID,CLASS,MenuName";
            string strSQL = "SELECT T_ImageProductJX.ENTITYNAME,ALIGN,PERIOD,AliasName,T_ENTITY.HINT,CLASS,cssName,MenuName,flag,T_UserAccess.IsAccess FROM T_ImageProductJX LEFT OUTER JOIN T_ENTITY ON T_ENTITY.ENTITYNAME = T_ImageProductJX.ENTITYNAME LEFT OUTER JOIN T_UserAccess ON T_UserAccess.ENTITYNAME = T_ImageProductJX.ENTITYNAME WHERE MODULENAME = '" + node + "' AND " + " T_UserAccess.IsAccess='0'  ORDER BY ORDERID,CLASS,MenuName";
            
            //return m_DatabaseJX.GetDataTable(strSQL);
            return m_Database.GetDataTable(strSQL);
        }

        public string GetImageProduct(string node)
        {
            //string nodeAuthority = GetAuthority();
            string nodeAuthority ="";

            IList<TreeNode> tree = new List<TreeNode>();

            //bool blnLeaf = true;
            string strSQL;
            if(nodeAuthority!="")
                strSQL = "SELECT T_ImageProduct.ENTITYNAME,ALIGN,PERIOD,AliasName,T_ENTITY.HINT,CLASS FROM T_ImageProduct LEFT OUTER JOIN T_ENTITY ON T_ENTITY.ENTITYNAME = T_ImageProduct.ENTITYNAME WHERE MODULENAME = '" + node + "' AND  T_ImageProduct.ENTITYNAME not in (" + nodeAuthority + ") ORDER BY CLASS,ORDERID";
            else
                strSQL = "SELECT T_ImageProduct.ENTITYNAME,ALIGN,PERIOD,AliasName,T_ENTITY.HINT,CLASS  FROM T_ImageProduct LEFT OUTER JOIN T_ENTITY ON T_ENTITY.ENTITYNAME = T_ImageProduct.ENTITYNAME WHERE MODULENAME = '" + node + "' ORDER BY CLASS,ORDERID";
            //�ӽڵ��ʱ����ͨ����|������ʾ��
            //if (node.Contains("|"))
            //{
            //    string[] strElements = node.Split('|');
            //    if(nodeAuthority!="")
            //        strSQL = "SELECT T_ImageProduct_test.ENTITYNAME,ALIGN,PERIOD,AliasName,T_ENTITY.HINT,CLASS = NULL  FROM T_ImageProduct_test LEFT OUTER JOIN T_ENTITY ON T_ENTITY.ENTITYNAME = T_ImageProduct_test.ENTITYNAME WHERE MODULENAME = '" + strElements[0] + "' AND CLASS = '" + strElements[1] + "' AND  T_ImageProduct_test.ENTITYNAME not in (" + nodeAuthority + ") ORDER BY ORDERID";
            //    else
            //        strSQL = "SELECT T_ImageProduct_test.ENTITYNAME,ALIGN,PERIOD,AliasName,T_ENTITY.HINT,CLASS= NULL  FROM T_ImageProduct_test LEFT OUTER JOIN T_ENTITY ON T_ENTITY.ENTITYNAME = T_ImageProduct_test.ENTITYNAME WHERE MODULENAME = '" + strElements[0] + "' AND CLASS = '" + strElements[1] + "' ORDER BY ORDERID";
            //}
            //DataTable dt = m_Database.GetDataTable(strSQL);
            SqlDataReader drProduct = m_Database.GetDataReader(strSQL);
            string nodeText = "";
            string jsonData="";
            StringBuilder sb = new StringBuilder("[");
            StringBuilder sm=new StringBuilder();
            if (drProduct.HasRows)
            {
                while (drProduct.Read())
                {
                    TreeNode treeNode = new TreeNode();
                    if (drProduct.IsDBNull(5))
                    {
                        //treeNode.id = drProduct.GetString(0) + "|" + drProduct.GetString(1);
                        //treeNode.text = drProduct.IsDBNull(4) ? "" : drProduct.GetString(4);
                        //treeNode.tag = drProduct.IsDBNull(2) ? "" : drProduct.GetString(2);
                        //treeNode.leaf = blnLeaf;
                        //treeNode.aliasName = drProduct.IsDBNull(3) ? "" : drProduct.GetString(3);
                    }
                    else
                    {

                        if (nodeText == "")
                        {
                            sm.Append("{");
                            nodeText = drProduct.GetString(5);
                            sm.AppendFormat("\"text\":\"{0}\",\"icon\":\"{1}\",", nodeText, "");
                            treeNode.id = drProduct.GetString(0);
                            treeNode.text = drProduct.IsDBNull(4) ? "" : drProduct.GetString(4);
                            treeNode.tag = drProduct.IsDBNull(2) ? "" : drProduct.GetString(2);
                            treeNode.aliasName = drProduct.IsDBNull(3) ? "" : drProduct.GetString(3);
                            tree.Add(treeNode);
                        }
                        else
                        {
                            string nodeTextNew = drProduct.GetString(5);
                            if (nodeText != nodeTextNew)
                            {
                                nodeText = nodeTextNew;
                                jsonData = JsonConvert.SerializeObject(tree);
                                sm.AppendFormat("\"children\":{0}", jsonData);
                                tree.Clear();
                                sm.Append("},{");
                                sm.AppendFormat("\"text\":\"{0}\",\"icon\":\"{1}\",", nodeTextNew, "");
                                treeNode.id = drProduct.GetString(0);
                                treeNode.text = drProduct.IsDBNull(4) ? "" : drProduct.GetString(4);
                                treeNode.tag = drProduct.IsDBNull(2) ? "" : drProduct.GetString(2);
                                treeNode.aliasName = drProduct.IsDBNull(3) ? "" : drProduct.GetString(3);
                                tree.Add(treeNode);
                            }
                            else
                            {
                                treeNode.id = drProduct.GetString(0);
                                treeNode.text = drProduct.IsDBNull(4) ? "" : drProduct.GetString(4);
                                treeNode.tag = drProduct.IsDBNull(2) ? "" : drProduct.GetString(2);
                                treeNode.aliasName = drProduct.IsDBNull(3) ? "" : drProduct.GetString(3);
                                tree.Add(treeNode);

                            }
                        }

                    }
                }
            }
            jsonData = JsonConvert.SerializeObject(tree);
            sm.AppendFormat("\"children\":{0}", jsonData);
            drProduct.Close();
            if (sm.Length > 1)
                sm.Append("}");
            sb.Append(sm.ToString());
            if (sb.Length > 1)
            {
                sb.Append("]");
            }
            else
                sb.Length = 0;

            return sb.ToString();
        }
        public string  GetAuthority()
        {
            string id = m_ID;
            string strSQL = "SELECT Authority From T_Classes WHERE ID="+id;
            DataTable dt = m_Database.GetDataTable(strSQL);
            string dataAuthority;
            if (dt.Rows[0][0].ToString() !="")
            {
                Authority m = (Authority)JsonConvert.DeserializeObject(dt.Rows[0][0].ToString(), typeof(Authority));//((Newtonsoft.Json.Linq.JContainer)(m)).First
                dataAuthority = m.data;
            }
            else
                dataAuthority = "";
            return dataAuthority;

        }
        public void setUserID(string ID)
        {
            m_ID = ID;

        }
        public IList<PropertyJsOV> GetEntityType(string entityName, string type)
        {
            Entity entity = new Entity(m_Database, entityName);
            IList fieldsProperty = entity.GetProperties(EntityStateContants.esQuery, QueryTypeContants.qtIndexQuery);
            PropertyOV fieldValue = null;
            IList<PropertyJsOV> properties = new List<PropertyJsOV>();
            for (int i = 0; i < fieldsProperty.Count; i++)
            {
                fieldValue = (PropertyOV)fieldsProperty[i];
                PropertyJsOV fieldJsOV = new PropertyJsOV();
                fieldJsOV.Alias = fieldValue.Alias;
                fieldJsOV.DefaultValue = fieldValue.DefaultValue;
                fieldJsOV.DictName = fieldValue.DictName;
                fieldJsOV.EntityName = fieldValue.EntityName;
                fieldJsOV.FieldType = (int)fieldValue.FieldType;
                fieldJsOV.IsEditable = fieldValue.IsEditable;
                fieldJsOV.IsEvent = fieldValue.IsEvent;
                fieldJsOV.IsNullable = fieldValue.IsNullable;
                fieldJsOV.IsPK = fieldValue.IsPK;
                fieldJsOV.Length = fieldValue.Length;
                fieldJsOV.Link = fieldValue.Link;
                fieldJsOV.Name = fieldValue.Name;
                fieldJsOV.OrderIndex = fieldValue.OrderIndex;
                fieldJsOV.QueryType = (int)fieldValue.QueryType;
                fieldJsOV.ShowType = (int)fieldValue.ShowType;
                if (fieldValue.FieldType == FieldTypeContants.ET_DATETIME)
                    fieldJsOV.ShowValue = GetLastestDatetime(entity,type);
                fieldJsOV.UpdatedValue = fieldValue.UpdatedValue;
                fieldJsOV.Value = fieldValue.Value;
                fieldJsOV.YField = fieldValue.YField;

                properties.Add(fieldJsOV);
            }
            return properties;
        }
        public IList<PropertyJsOV> GetEntity(string entityName)
        {
            Entity entity = new Entity(m_Database, entityName);
            IList fieldsProperty = entity.GetProperties(EntityStateContants.esQuery, QueryTypeContants.qtIndexQuery);
            PropertyOV fieldValue = null;
            IList<PropertyJsOV> properties = new List<PropertyJsOV>();
            for (int i = 0; i < fieldsProperty.Count; i++)
            {
                fieldValue = (PropertyOV)fieldsProperty[i];
                PropertyJsOV fieldJsOV = new PropertyJsOV();
                fieldJsOV.Alias = fieldValue.Alias;
                fieldJsOV.DefaultValue = fieldValue.DefaultValue;
                fieldJsOV.DictName = fieldValue.DictName;
                fieldJsOV.EntityName = fieldValue.EntityName;
                fieldJsOV.FieldType = (int)fieldValue.FieldType;
                fieldJsOV.IsEditable = fieldValue.IsEditable;
                fieldJsOV.IsEvent = fieldValue.IsEvent;
                fieldJsOV.IsNullable = fieldValue.IsNullable;
                fieldJsOV.IsPK = fieldValue.IsPK;
                fieldJsOV.Length = fieldValue.Length;
                fieldJsOV.Link = fieldValue.Link;
                fieldJsOV.Name = fieldValue.Name;
                fieldJsOV.OrderIndex = fieldValue.OrderIndex;
                fieldJsOV.QueryType = (int)fieldValue.QueryType;
                fieldJsOV.ShowType = (int)fieldValue.ShowType;
                if (fieldValue.FieldType == FieldTypeContants.ET_DATETIME)
                    fieldJsOV.ShowValue = GetLastestDatetime(entity);
                fieldJsOV.UpdatedValue = fieldValue.UpdatedValue;
                fieldJsOV.Value = fieldValue.Value;
                fieldJsOV.YField = fieldValue.YField;

                properties.Add(fieldJsOV);
            }
            return properties;
        }
        /// <summary>
        /// ��ȡʵ�嵱ǰ
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private string GetLastestDatetime(Entity entity)
        {
            string strWhere = "SELECT COUNT(*) FROM T_ImageProduct1 WHERE ENTITYNAME = '" + entity.Name + "' AND PERIOD IS NULL";
            string strExisit = m_Database.GetFirstValue(strWhere);
            string strGetLastestDatetime = "";
            IList fieldsProperty = entity.GetProperties(EntityStateContants.esQuery, QueryTypeContants.qtIndexQuery);
            strWhere = "SELECT MAX(FORECASTDATE) FROM " + entity.TableName;
            if (entity.Condition != "")
            {
                strWhere = strWhere + " WHERE " + entity.Condition;
            }
            SqlDataReader dr = m_Database.GetDataReader(strWhere);
            if (dr.Read())
            {
                if (dr.IsDBNull(0) == false)
                {
                    DateTime dt = dr.GetDateTime(0);
                    if (strExisit == "1")
                        if (entity.OperatorType == OperatorTypeContants.otStatistic)//����Pm10��Pm2.5���������ݲ���һСʱ
                            strGetLastestDatetime = dt.ToString("yyyy-MM-dd HH:mm:ss");
                        else//��ǰֻ̨����ʾ��ʱ�����ʵ�������д���ʱ�ֵ�������������ݾͲ��ᱻ�鵽���������һСʱ
                            strGetLastestDatetime = dt.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss");
                    else
                        strGetLastestDatetime = dt.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
            dr.Close();

            if (strGetLastestDatetime == "")
            {
                string strPeriod = GetPeriod(entity.Name);
                if (strPeriod != "")
                {
                    string[] periods = strPeriod.Split(',');
                    strGetLastestDatetime = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd " + periods[periods.Length - 1] + ":00:00");
                }
                else
                    strGetLastestDatetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            }

            return strGetLastestDatetime;
        }
        private string GetLastestDatetime(Entity entity,string type)
        {
            string strWhere = "SELECT COUNT(*) FROM T_ImageProduct1 WHERE ENTITYNAME = '" + entity.Name + "' AND PERIOD IS NULL";
            string strExisit = m_Database.GetFirstValue(strWhere);
            string strGetLastestDatetime = "";
            IList fieldsProperty = entity.GetProperties(EntityStateContants.esQuery, QueryTypeContants.qtIndexQuery);
            strWhere = "SELECT MAX(FORECASTDATE) FROM " + entity.TableName;
            if (entity.Condition != "")
            {
                strWhere = strWhere + " WHERE " + entity.Condition;
            }
            if (type != "")
            {
                if(type.IndexOf(",")>0)
                    strWhere = strWhere + " AND  Type in" + returnSQLStr(type); 
                else
                    strWhere = strWhere + " AND  Type='" + type+"'"; 

            }
            SqlDataReader dr = m_Database.GetDataReader(strWhere);
            if (dr.Read())
            {
                if (dr.IsDBNull(0) == false)
                {
                    DateTime dt = dr.GetDateTime(0);
                    if (strExisit == "1")
                        if (entity.OperatorType == OperatorTypeContants.otStatistic)//����Pm10��Pm2.5���������ݲ���һСʱ
                            strGetLastestDatetime = dt.ToString("yyyy-MM-dd HH:mm:ss");
                        else//��ǰֻ̨����ʾ��ʱ�����ʵ�������д���ʱ�ֵ�������������ݾͲ��ᱻ�鵽���������һСʱ
                            strGetLastestDatetime = dt.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss");
                    else
                        strGetLastestDatetime = dt.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
            dr.Close();

            if (strGetLastestDatetime == "")
            {
                string strPeriod = GetPeriod(entity.Name);
                if (strPeriod != "")
                {
                    string[] periods = strPeriod.Split(',');
                    strGetLastestDatetime = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd " + periods[periods.Length - 1] + ":00:00");
                }
                else
                    strGetLastestDatetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            }

            return strGetLastestDatetime;
        }
        //�л�ʱ���ѯ
        public DataTable QueryTextList(string entityName, string entityObj)
        {

            Entity entity = new Entity(m_Database, entityName);
            string strWhere = " WHERE ";
            if (entityObj != "")
                strWhere = strWhere + GetUserWhere(entity, entityObj);
            else
                strWhere = strWhere + GetDefaultWhere(entity);

            if (entity.Condition != "")
            {
                strWhere = strWhere + " AND " + entity.Condition;
            }

            try
            {
                string strSQL = "SELECT distinct CONVERT(varchar(16),DATEADD(hour, CONVERT(int, Period), ForecastDate), 120) AS MC FROM  " + entity.TableName + strWhere + "ORDER BY MC ";
                DataTable dt = m_Database.GetDataTable(strSQL);
                return dt;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public string trickText(string Datetime, string entityName)
        {
            IList<PropertyJsOV> properties = new List<PropertyJsOV>();
            properties = GetEntity(entityName);
            Entity entity = new Entity(m_Database, entityName);
            string strWhere = " WHERE  ForecastDate='"+Datetime+"'";
            string entityObj = ToJSON(properties);
            string strSQL = "SELECT  (folder + '/' + name) AS DM, ForecastDate  AS MC FROM  " + entity.TableName + strWhere;
            string url = m_Database.GetFirstValue(strSQL);
            url = ConfigurationManager.AppSettings["testFileLoad"] + url.Replace("/", "\\");
            int index = url.IndexOf('?');
            url = url.Substring(0, index);
            StreamReader sr = File.OpenText(url);
            string nextLine = sr.ReadLine();
            nextLine = sr.ReadLine();
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0}*", nextLine);
            nextLine = sr.ReadLine();
            sb.AppendFormat("{0}*", nextLine);
            while ((nextLine = sr.ReadLine()) != null)
            {
                sb.AppendFormat("<p>{0}</p>", nextLine);
            } 
            sr.Close();
            return sb.ToString();
        }
        public string trickQueryEastList(string Datetime, string entityName, string json, string period)
        {
            if (Datetime != "")
            {
                string[] info = json.Substring(1, json.Length - 2).Split(new char[] { ';', ':' });
                //DataTable dTable = QueryDataTable(Datetime, entityName, json,period);
                DataTable dTable = QueryDataTableForMM(Datetime, entityName, json, period);
                try
                {
                    string imgUrl = QueryImgUrlEast(dTable, entityName, info);
                    return imgUrl;
                }
                catch (Exception e)
                {
                    throw e;
                }
            }
            else
                return "";
        }
        public string trickQueryList(string Datetime, string entityName, string json)
        {
            if (Datetime != "")
            {
                string[] info = json.Substring(1, json.Length - 2).Split(new char[] { ';', ':' });
                DataTable dTable= QueryDataTable(Datetime,entityName,json,"");
                try
                {
                    string imgUrl = QueryImgUrl(dTable, entityName, info);
                    return imgUrl;
                }
                catch (Exception e)
                {
                    throw e;
                }
            }
            else 
                return "";
        }
        //����ģʽ��������
        public string trickQueryListMM(string Datetime, string entityName, string json)
        {
            if (Datetime != "")
            {
                string[] info = json.Substring(1, json.Length - 2).Split(new char[] { ';', ':' });
                DataTable dTable = QueryDataTableForMM(Datetime, entityName, json, "");
                try
                {
                    string imgUrl = QueryImgUrl(dTable, entityName, info);
                    return imgUrl;
                }
                catch (Exception e)
                {
                    throw e;
                }
            }
            else
                return "";
        }
        public DataTable QueryDataTable(string Datetime, string entityName, string json,string period)
        {
            DataTable dTable=new DataTable();
            if (Datetime != "")
            {
                string[] info = json.Substring(1, json.Length - 2).Split(new char[] { ';', ':' });
                IList<PropertyJsOV> properties = new List<PropertyJsOV>();
                properties = GetEntity(entityName);
                StringBuilder sb = new StringBuilder();
                Entity entity = new Entity(m_Database, entityName);
                string entityObj = ToJSON(properties);
                string strWhere = "";
               
                if (info[7] != "" && info[1] != "ȫ��" && info[1] != "����")
                {                    
                    if (info[1] != "")
                    {
                        int periods = int.Parse(period);
                        string forecastDate = DateTime.Parse(Datetime).AddHours(-periods).ToString("yyyy-MM-dd HH:00:00");
                        strWhere = " WHERE ForecastDate='" + forecastDate + "' AND Period='" + period + "'";
                    }
                    else
                        strWhere = " WHERE ForecastDate='" + Datetime + "'";
                    strWhere = strWhere + "  AND Type in " + returnSQLStr(info[7]);
                }
                else
                    strWhere = " WHERE ForecastDate<='" + Datetime + "'";
                if (entity.Condition != "")
                    strWhere = strWhere + " AND " + entity.Condition;
                if(info[1] == "ȫ��" || info[1] == "����")
                    strWhere = strWhere + " AND  station='" + info[7] +"' ";

                int counts = int.Parse(info[3]);
                int allCounts = int.Parse(info[5]);
                double width = 100.0 / counts - 1.0;
                int TypeLength = info[7].Split(',').Length;               

                
                 
                       

                string strSQL = "SELECT TOP(" + allCounts + ") ('Product/' + folder + '/' + name) AS DM, ForecastDate  AS MC,TYPE,Period FROM  " + entity.TableName + strWhere + "ORDER BY MC DESC";
                //string strSQL = "SELECT TOP(" + allCounts + ") ('Product/' + folder + '/' + name) AS DM, ForecastDate  AS MC,TYPE,Period FROM  " + entity.TableName + strWhere + strTypeSQL;

                dTable = m_Database.GetDataTable(strSQL);
                return dTable;
            }
            else
                return dTable;
                
        }

        //ģʽ�����ķ���
        public DataTable QueryDataTableForMM(string Datetime, string entityName, string json, string period)
        {
            DataTable dTable = new DataTable();
            if (Datetime != "")
            {
                string[] info = json.Substring(1, json.Length - 2).Split(new char[] { ';', ':' });
                IList<PropertyJsOV> properties = new List<PropertyJsOV>();
                properties = GetEntity(entityName);
                StringBuilder sb = new StringBuilder();
                Entity entity = new Entity(m_Database, entityName);
                string entityObj = ToJSON(properties);
                string strWhere = "";
                //���ݿ��������ַ�����Type�����ֵĸ���
                string strTypeSQL = "";
                if (info[7] != "" && info[1] != "ȫ��" && info[1] != "����")
                {
                    strTypeSQL = "ORDER BY  CHARINDEX(Type," + "'" + info[7] + "'" + ")";
                    if (info[1] != "")
                    {
                        int periods = int.Parse(period);
                        string forecastDate = DateTime.Parse(Datetime).AddHours(-periods).ToString("yyyy-MM-dd HH:00:00");
                        strWhere = " WHERE ForecastDate='" + forecastDate + "' AND Period='" + period + "'";
                    }
                    else
                        strWhere = " WHERE ForecastDate='" + Datetime + "'";
                    strWhere = strWhere + "  AND Type in " + returnSQLStr(info[7]);
                }
                else
                    strWhere = " WHERE ForecastDate<='" + Datetime + "'";
                if (entity.Condition != "")
                    strWhere = strWhere + " AND " + entity.Condition;
                if (info[1] == "ȫ��" || info[1] == "����")
                    strWhere = strWhere + " AND  station='" + info[7] + "' ";

                int counts = int.Parse(info[3]);
                int allCounts = int.Parse(info[5]);
                double width = 100.0 / counts - 1.0;
                int TypeLength = info[7].Split(',').Length;





                //string strSQL = "SELECT TOP(" + allCounts + ") ('Product/' + folder + '/' + name) AS DM, ForecastDate  AS MC,TYPE,Period FROM  " + entity.TableName + strWhere + "ORDER BY MC DESC";
                string strSQL = "SELECT TOP(" + allCounts + ") ('Product/' + folder + '/' + name) AS DM, ForecastDate  AS MC,TYPE,Period FROM  " + entity.TableName + strWhere + strTypeSQL;

                dTable = m_Database.GetDataTable(strSQL);
                return dTable;
            }
            else
                return dTable;

        }
        public string QueryImgUrlEast(DataTable dTable, string entityName, string[] info)
        {
            StringBuilder sb = new StringBuilder();
            int counts = int.Parse(info[3]);
            int allCounts = int.Parse(info[5]);
            double width = 0.0;
            int imgSpan = 0;
            if (info[9] != "")
            {
                //imgSpan = 3;
                imgSpan = 1;
                if (dTable.Rows.Count <= counts & dTable.Rows.Count > 2)
                    width = (100.0 + imgSpan) / dTable.Rows.Count - imgSpan;
                else
                {
                    if (info[1] == "1")
                    {
                        if (info.Length > 10)
                            width = int.Parse(info[11]);
                        else 
                            width = 65;
                    }
                    else if (info.Length > 10)
                        width = int.Parse(info[11]);
                    else
                        width = 40;
                }
            }
            else
            {
                if (info[1] == "1")
                {
                    if (dTable.Rows.Count <= counts & dTable.Rows.Count > 2)
                    {
                        imgSpan = 3;
                        width = (100.0 + imgSpan) / dTable.Rows.Count - imgSpan;
                    }
                    else
                    {
                        if (info.Length > 10)
                            width = int.Parse(info[11]);
                        else
                            width = 65;
                    }
                }
                else
                {
                    imgSpan = 10;
                    width = (100.0 + imgSpan) / counts - imgSpan;
                }

            }

            if (dTable.Rows.Count < 4)
            {
                imgSpan = 0;
            }

            for (int i = 0; i <Math.Ceiling(double.Parse(dTable.Rows.Count.ToString()) / counts); i++)
            {
                if (entityName == "HysplitBackward")
                    sb.AppendFormat("<div style='margin-top:80px;text-align:center;margin-right:auto;margin-left:auto'>", imgSpan.ToString());
                else
                {
                    if (info[9] != "")
                    {
                        sb.AppendFormat("<div style='margin-top:7px;text-align:center;margin-right:auto;margin-left:auto'>", imgSpan.ToString());
                    }
                    else
                    {
                        if (info[1] == "1")
                            sb.AppendFormat("<div style='margin-top:7px;text-align:center;margin-right:auto;margin-left:auto'>", imgSpan.ToString());
                        else 
                            sb.AppendFormat("<div style='margin-top:7px;margin-right:{0}%;margin-left:{0}%'>", imgSpan.ToString());
                    }
                }
                for (int j = 0; j < counts; j++)
                {
                    if (i * counts + j + 1 <= allCounts)
                    {
                        if ((i * counts + j) >= dTable.Rows.Count)
                        {
                            if (info[9] == "")
                                sb.AppendFormat("<img src='{0}' width='{1}' onclick=\\\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\\\" style='margin-right:{7}%'/>", "", width.ToString() + "%", entityName, "", "", "", "", imgSpan.ToString());                                
                            continue;
                        }
                        if (j != counts - 1)                            
                            sb.AppendFormat("<img src='{0}' width='{1}' onclick=\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\" style='margin-right:{7}%;'/>", dTable.Rows[i * counts + j][0], width.ToString() + "%", entityName, "", dTable.Rows[i * counts + j][1], dTable.Rows[i * counts + j][2], dTable.Rows[i * counts + j][3], imgSpan.ToString());
                        else
                            sb.AppendFormat("<img src='{0}' width='{1}'  onclick=\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\" />", dTable.Rows[i * counts + j][0], width.ToString() + "%", entityName, "", dTable.Rows[i * counts + j][1], dTable.Rows[i * counts + j][2], dTable.Rows[i * counts + j][3]);
                        
                    }
                }
                sb.Append("</div>");
            }
            return sb.ToString();
        }
        public string QueryImgUrl(DataTable dTable,string entityName,string[]info)
        {
            StringBuilder sb = new StringBuilder();
            int counts = int.Parse(info[3]);
            int allCounts = int.Parse(info[5]);
            double width = 102.0 / counts -3;
            for (int i = 0; i <= allCounts; i++)
            {
                sb.Append("<div style='margin-bottom:7px'>");
                for (int j = 0; j < counts; j++)
                {
                    if (i * counts + j + 1 <= allCounts)
                    {
                        if ((i * counts + j) >= dTable.Rows.Count)
                        {
                            sb.AppendFormat("<img src='{0}' width='{1}' onclick=\\\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\\\" style='margin-right:3%'/>", "", width.ToString() + "%", entityName, "", "", "","");
                            continue;
                        }
                        if (j != counts - 1)
                            sb.AppendFormat("<img src='{0}' width='{1}' onclick=\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\" style='margin-right:3%'/>", dTable.Rows[i * counts + j][0], width.ToString() + "%", entityName, "", dTable.Rows[i * counts + j][1], dTable.Rows[i * counts + j][2], dTable.Rows[i * counts + j][3]);
                        else
                            sb.AppendFormat("<img src='{0}' width='{1}'  onclick=\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\" />", dTable.Rows[i * counts + j][0], width.ToString() + "%", entityName, "", dTable.Rows[i * counts + j][1], dTable.Rows[i * counts + j][2], dTable.Rows[i * counts + j][3]);
                    }
                }
                sb.Append("</div>");
            }
            return sb.ToString();
        }
        public string returnSQLStr(string type)
        {
            string module="(";
            string[] types = type.Split(',');
            for (int i = 0; i < types.Length; i++)
            {
                module = module + "'" + types[i] + "',";
            }
            module = module.Substring(0, module.Length - 1) + ")";
            return module;
        }
        public string ECCity(string entityName, string type, string column, string totalCount, string period)
        {
            IList<PropertyJsOV> properties = new List<PropertyJsOV>();
            properties = GetEntity(entityName);
            Entity entity = new Entity(m_Database, entityName);
            string forecastDate = GetLastestDatetime(entity);
            int counts = int.Parse(column);
            int allCounts = int.Parse(totalCount);
            double width = 100.0 / counts - 1.0;
            StringBuilder sb = new StringBuilder("\"");
            string entityObj = ToJSON(properties);
            string strWhere = " WHERE ForecastDate<='" + forecastDate + "' AND Type='"+type+"'";
            if (entity.Condition != "")
                strWhere = strWhere + " AND " + entity.Condition;
            string strSQL = "SELECT TOP(" + allCounts + ") ('Product/' + folder + '/' + name) AS DM, ForecastDate  AS MC,TYPE,Period FROM  " + entity.TableName + strWhere + "ORDER BY MC DESC";
            DataTable dt2 = m_Database.GetDataTable(strSQL);
            string jsonStr = "";
            for (int i = 0; i <= allCounts; i++)
            {
                sb.Append("<div style='margin-bottom:7px'>");
                for (int j = 0; j < counts; j++)
                {
                    if (i * counts + j + 1 <= allCounts)
                    {
                        if ((i * counts + j) >= dt2.Rows.Count)
                        {
                            sb.AppendFormat("<img src='{0}' width='{1}' onclick=\\\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\\\" style='margin-right:10px'/>", "", width.ToString() + "%", entityName, "", "", "", "");
                            continue;
                        }
                        if (j == counts - 1)
                            sb.AppendFormat("<img src='{0}' width='{1}' onclick=\\\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\\\" style='margin-right:10px'/>", dt2.Rows[i * counts + j][0], width.ToString() + "%", entityName, "", dt2.Rows[i * counts + j][1], dt2.Rows[i * counts + j][2], dt2.Rows[i * counts + j][3]);
                        else
                            sb.AppendFormat("<img src='{0}' width='{1}'  onclick=\\\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\\\" />", dt2.Rows[i * counts + j][0], width.ToString() + "%", entityName, "", dt2.Rows[i * counts + j][1], dt2.Rows[i * counts + j][2], dt2.Rows[i * counts + j][3]);
                    }
                }
                sb.Append("</div>");
            }
            jsonStr = jsonStr + "{\"imgHtml\":" + sb.Append("\"}");
            return jsonStr;
        }
        public string AirQualityQueryList(string entityName, string json)
        {
            string[] entityNameArray = entityName.Split(',');
            string strSQL = "";
            Entity entity;
            string[] info = json.Substring(1, json.Length - 2).Split(new char[] { ';', ':' });
            string strWhere = " ";
            int counts = int.Parse(info[3]);
            int imgCount = int.Parse(info[5]);
            for (int i = 0; i < entityNameArray.Length; i++)
            {
                entity = new Entity(m_Database, entityNameArray[i]);
                strWhere = " WHERE ";
              
                if (entity.Condition != "")
                    strWhere = strWhere  + entity.Condition;
                if(info[7]!="")
                    strWhere = strWhere + " AND  Station='" + info[7] + "'";
                if (entityNameArray.Length == 1)
                    strSQL = strSQL + " SELECT TOP(" + imgCount + ") ('Product/' + folder + '/' + name) AS DM, ForecastDate  AS MC,TYPE,Period,'" + entityNameArray[i] + "' AS entityName FROM  " + entity.TableName + strWhere + " ORDER BY ForecastDate DESC  union ";
                else
                    strSQL = strSQL + " SELECT * FROM ( SELECT TOP(1)('Product/' + folder + '/' + name) AS DM, ForecastDate  AS MC,TYPE,Period,'" + entityNameArray[i] + "' AS entityName FROM  " + entity.TableName + strWhere + " ORDER BY  ForecastDate DESC) as  a"+i+" union all";         
            }
            int index = strSQL.LastIndexOf("union");
            if (entityNameArray.Length!= 1)
                strSQL = strSQL.Substring(0, index) + " ORDER BY  type";
            else
                strSQL = strSQL.Substring(0, index);
            DataTable dt2 = m_Database.GetDataTable(strSQL);


            int allCounts = dt2.Rows.Count;
            double width = 100.0 / counts - 1.0;
            StringBuilder sb = new StringBuilder();
            if (dt2.Rows.Count == 1)
            {
                if (info[7]=="01")
                    width = 60;
                else
                    width = 40;
                sb.Append("<div style='text-align:center;'>");
                sb.AppendFormat("<img src='{0}' width='{1}'  onclick=\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\" />", dt2.Rows[0][0], width.ToString() + "%", entityName, "", dt2.Rows[0][1], dt2.Rows[0][2], dt2.Rows[0][3]);
                sb.Append("</div>");

            }
            else
            {
                for (int i = 0; i <= allCounts; i++)
                {
                    sb.Append("<div style='margin-bottom:7px'>");
                    for (int j = 0; j < counts; j++)
                    {
                        if (i * counts + j + 1 <= allCounts)
                        {
                            if ((i * counts + j) >= dt2.Rows.Count)
                            {
                                sb.AppendFormat("<img src='{0}' width='{1}' onclick=\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\" style='margin-right:10px'/>", "", width.ToString() + "%", dt2.Rows[i * counts + j][4], "", "", "", "");
                                continue;
                            }
                            if (j == counts - 1)
                                sb.AppendFormat("<img src='{0}' width='{1}' onclick=\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\" style='margin-right:10px'/>", dt2.Rows[i * counts + j][0], width.ToString() + "%", dt2.Rows[i * counts + j][4], "", dt2.Rows[i * counts + j][1], dt2.Rows[i * counts + j][2], dt2.Rows[i * counts + j][3]);
                            else
                                sb.AppendFormat("<img src='{0}' width='{1}'  onclick=\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\" />", dt2.Rows[i * counts + j][0], width.ToString() + "%", dt2.Rows[i * counts + j][4], "", dt2.Rows[i * counts + j][1], dt2.Rows[i * counts + j][2], dt2.Rows[i * counts + j][3]);
                        }
                    }
                    sb.Append("</div>");
                }
            }
            string jsonStr = sb.ToString();
            return jsonStr;           
        }
        public string PublicQueryListPast(string entityName,string json)
        {
            string[] info = json.Substring(1, json.Length - 2).Split(new char[] { ';',':' });
            IList<PropertyJsOV> properties = new List<PropertyJsOV>();
            properties = GetEntity(entityName);
            string forecastDate = properties[0].ShowValue;
            string startTime = DateTime.Parse(forecastDate).AddDays(-10).ToString("yyyy-MM-dd HH:00:00");
            StringBuilder sb = new StringBuilder();
            string entityObj = ToJSON(properties);
            
            DataTable timeList = new DataTable();//ʱ���б�
            DataTable dt2 = new DataTable();//����
            if (info[7] != ""&& info[1] != "ȫ��" && info[1] != "����")
            {
                if (info[1] != "")
                   dt2= QueryModuleList(entityName,json);
                else 
                    dt2 = QueryList(entityName, entityObj);
                properties[0].ShowValue = startTime + "||" + forecastDate;
                entityObj = ToJSON(properties);
                timeList = QueryTimeList(entityName, entityObj,json);
                DataView dv = timeList.DefaultView;
                dv.Sort = "MC DESC";
                timeList = dv.ToTable();
                sb.Append("<label class='cur-select' id='labelSelect'>" + timeList.Rows[0][0] + "</label>");
                sb.Append("<select id='selectID'>");
                int k = 0;
                foreach (DataRow rows in timeList.Rows)
                {
                    if (k == 0)
                    {
                        sb.AppendFormat("<option value ='{0}' selected='true'>{0}</option>", rows[0]);
                        k = 1;
                    }
                    else
                        sb.AppendFormat("<option value ='{0}' >{0}</option>", rows[0]);
                }
            }
            else
            {
                properties[0].ShowValue = startTime + "||" + forecastDate;
                entityObj = ToJSON(properties);
                if (info[1] != "")
                    timeList = QueryListAirQuality(entityName, entityObj,info[7]);
                else 
                    timeList = QueryList(entityName, entityObj);
                DataView dv = timeList.DefaultView;
                dv.Sort = "MC DESC";
                dt2 = dv.ToTable();
                //sb.Append("<label class='cur-select' id='labelSelect'>" + dt2.Rows[0][1] + "</label>");
                //sb.Append("<select id='selectID'>");


                sb.Append("<select id='selectID'>" + dt2.Rows[0][1]);
                int k = 0;
                foreach (DataRow rows in dt2.Rows)
                {
                    if (k == 0)
                    {
                        sb.AppendFormat("<option value ='{0}' selected='true'>{0}</option>", rows[1]);
                        k = 1;
                    }
                    else
                        sb.AppendFormat("<option value ='{0}' >{0}</option>", rows[1]);
                }
            }           
            sb.Append("</select>");
            string jsonStr = "{\"date\":\"" + sb.ToString() + "\",";
            int counts = int.Parse(info[3]);
            int allCounts = int.Parse(info[5]);
            double width = 100.0 / counts - 1.0;
            sb = new StringBuilder("\"");
            for (int i = 0; i <= allCounts; i++)
            {
                sb.Append("<div style='margin-bottom:7px'>");
                for (int j = 0; j < counts; j++)
                {
                    if (i * counts + j + 1 <= allCounts)
                    {
                        if ((i * counts + j) >= dt2.Rows.Count)
                        {
                            sb.AppendFormat("<img src='{0}' width='{1}' onclick=\\\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\\\" style='margin-right:10px'/>", "", width.ToString() + "%", entityName, "", "", "","");
                            continue;
                        }
                        if (j == counts - 1)
                            sb.AppendFormat("<img src='{0}' width='{1}' onclick=\\\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\\\" style='margin-right:10px'/>", dt2.Rows[i * counts + j][0], width.ToString() + "%", entityName, "", dt2.Rows[i * counts + j][1], dt2.Rows[i * counts + j][2], dt2.Rows[i * counts + j][3]);
                        else
                            sb.AppendFormat("<img src='{0}' width='{1}'  onclick=\\\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\\\" />", dt2.Rows[i * counts + j][0], width.ToString() + "%", entityName, "", dt2.Rows[i * counts + j][1], dt2.Rows[i * counts + j][2], dt2.Rows[i * counts + j][3]);
                    }
                }
                sb.Append("</div>");
            }
            jsonStr = jsonStr + "\"contentNone\":" + sb.Append("\"}");
            return jsonStr;

        }

        //��select�����˵���дΪul li��ʽ
        public string PublicQueryList(string entityName, string json)
        {
            string[] info = json.Substring(1, json.Length - 2).Split(new char[] { ';', ':' });
            IList<PropertyJsOV> properties = new List<PropertyJsOV>();
            properties = GetEntity(entityName);
            string forecastDate = properties[0].ShowValue;
            string startTime = DateTime.Parse(forecastDate).AddDays(-10).ToString("yyyy-MM-dd HH:00:00");
            StringBuilder sb = new StringBuilder();
            string entityObj = ToJSON(properties);

            DataTable timeList = new DataTable();//ʱ���б�
            DataTable dt2 = new DataTable();//����
            if (info[7] != "" && info[1] != "ȫ��" && info[1] != "����" && info[1] != "����")
            {
                if (info[1] != "")
                    dt2 = QueryModuleList(entityName, json);
                else
                    dt2 = QueryList(entityName, entityObj);
                properties[0].ShowValue = startTime + "||" + forecastDate;
                entityObj = ToJSON(properties);
                timeList = QueryTimeList(entityName, entityObj, json);
                DataView dv = timeList.DefaultView;
                dv.Sort = "MC DESC";
                timeList = dv.ToTable();
                sb.Append("<label class='cur-select' id='labelSelect'>" + timeList.Rows[0][0] + "</label>");
                sb.Append("<select id='selectID'>");
                int k = 0;
                foreach (DataRow rows in timeList.Rows)
                {
                    if (k == 0)
                    {
                        sb.AppendFormat("<option value ='{0}' selected='true'>{0}</option>", rows[0]);
                        k = 1;
                    }
                    else
                        sb.AppendFormat("<option value ='{0}' >{0}</option>", rows[0]);
                }
            }
            else
            {
                properties[0].ShowValue = startTime + "||" + forecastDate;
                entityObj = ToJSON(properties);
                if (info[1] != "")
                    timeList = QueryListAirQuality(entityName, entityObj, info[7]);
                else
                    timeList = QueryList(entityName, entityObj);
                DataView dv = timeList.DefaultView;
                dv.Sort = "MC DESC";
                dt2 = dv.ToTable();
                //sb.Append("<label class='cur-select' id='labelSelect'>" + dt2.Rows[0][1] + "</label>");
                //sb.Append("<select id='selectID'>");


                sb.Append("<div id='selectID' class='dateDiv'>" +"<div id='dateTxt'>"+ dt2.Rows[0][1] +"</div>"+ "<div id='selIcon' class='selIcon'></div></div>");
                int k = 0;
                sb.Append("<ul id='dateUl' class='dateUl'>");
                foreach (DataRow rows in dt2.Rows)
                {
                    if (k == 0)
                    {
                        sb.AppendFormat("<li>{0}</li>", rows[1]);
                        k = 1;
                    }
                    else
                        sb.AppendFormat("<li>{0}</li>", rows[1]);
                }
            }
            sb.Append("</ul>");
            string jsonStr = "{\"date\":\"" + sb.ToString() + "\",";
            int counts = int.Parse(info[3]);
            int allCounts = int.Parse(info[5]);
            double width = 100.0 / counts - 1.0;
            sb = new StringBuilder("\"");
            for (int i = 0; i <= allCounts; i++)
            {
                sb.Append("<div style='margin-bottom:7px'>");
                for (int j = 0; j < counts; j++)
                {
                    if (i * counts + j + 1 <= allCounts)
                    {
                        if ((i * counts + j) >= dt2.Rows.Count)
                        {
                            sb.AppendFormat("<img src='{0}' width='{1}' onclick=\\\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\\\" style='margin-right:10px'/>", "", width.ToString() + "%", entityName, "", "", "", "");
                            continue;
                        }
                        if (j == counts - 1)
                            sb.AppendFormat("<img src='{0}' width='{1}' onclick=\\\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\\\" style='margin-right:10px'/>", dt2.Rows[i * counts + j][0], width.ToString() + "%", entityName, "", dt2.Rows[i * counts + j][1], dt2.Rows[i * counts + j][2], dt2.Rows[i * counts + j][3]);
                        else
                            sb.AppendFormat("<img src='{0}' width='{1}'  onclick=\\\"showOne('{2}','{3}','{0}','{4}','{5}','{6}')\\\" />", dt2.Rows[i * counts + j][0], width.ToString() + "%", entityName, "", dt2.Rows[i * counts + j][1], dt2.Rows[i * counts + j][2], dt2.Rows[i * counts + j][3]);
                    }
                }
                sb.Append("</div>");
            }
            jsonStr = jsonStr + "\"contentNone\":" + sb.Append("\"}");
            return jsonStr;

        }

        //����ʱ���б����ݻ�ȡ
        public DataTable QueryTimeList(string entityName, string entityObj,string json)
        {
            string[] info = json.Substring(1, json.Length - 2).Split(new char[] { ';', ':' });

            Entity entity = new Entity(m_Database, entityName);
            string strWhere = " WHERE ";
            if (entityObj != "")
                strWhere = strWhere + GetUserWhere(entity, entityObj);
            else
                strWhere = strWhere + GetDefaultWhere(entity);

            if (entity.Condition != "")
            {
                strWhere = strWhere + " AND " + entity.Condition;
            }
            if (info[1] != "ȫ��" && info[1] != "����")
            {
                if (info[7] != "")
                {
                    if (info[7].IndexOf(",") <= 0)
                        strWhere = strWhere + "  AND Type = '" + info[7] + "'";
                    else
                        strWhere = strWhere + "  AND Type in " + returnSQLStr(info[7]);
                }
            }
            try
            {
                string strSQL = "SELECT distinct CONVERT(varchar(16),DATEADD(hour, CONVERT(int, Period), ForecastDate), 120) AS MC,Period AS DM FROM  " + entity.TableName + strWhere + "ORDER BY MC ";
                DataTable dt = m_Database.GetDataTable(strSQL);
                return dt;
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }
        //ģʽ���ݲ�ѯ�б�
        public DataTable QueryModuleList(string entityName,string json)
        {
            string[] info = json.Substring(1, json.Length - 2).Split(new char[] { ';', ':' });
            IList<PropertyJsOV> properties = new List<PropertyJsOV>();
            properties = GetEntity(entityName);
            DataTable dt = new DataTable();
            string forecastDate = properties[0].ShowValue;
            Entity entity = new Entity(m_Database, entityName);
            string strWhere =" WHERE   ForecastDate='" + forecastDate + "'";
            if (entity.Condition != "")
                strWhere = strWhere +" AND " +entity.Condition;//+ " AND " 
            strWhere = strWhere + "  AND Type in " + returnSQLStr(info[7]);
            string strSQL = "SELECT TOP(" + info[5] + ") ('Product/' + folder + '/' + name) AS DM, ForecastDate AS MC,Type,Period FROM   " + entity.TableName + strWhere + "ORDER BY ForecastDate,Period DESC";
            dt = m_Database.GetDataTable(strSQL);
            return dt;
        }
        public string AirQualityBottomSelect(string Datetime, string entityName, string json, string type)
        {
            string[] info = json.Substring(1, json.Length - 2).Split(new char[] { ';', ':' });
            StringBuilder sb = new StringBuilder("{");
            DataTable dt = new DataTable();
            int hour = DateTime.Parse(Datetime).Hour;
            string startTime = DateTime.Parse(Datetime).ToString("yyyy-MM-dd 00:00:00");
            string endTime = DateTime.Parse(Datetime).ToString("yyyy-MM-dd 23:59:59");
            Entity entity = new Entity(m_Database, entityName);
            string strWhere = "  WHERE ";
            if (entity.Condition != "")
                strWhere = strWhere + entity.Condition + " AND ";//+ " AND " 
            if (info[7] != "")
                strWhere = strWhere + " Station='" + info[7] + "'  AND ";
            if (info[9] == "")
            {
                strWhere = strWhere + "   ForecastDate BETWEEN'" + startTime + "' AND '" + endTime + "'";
                string strSQL = "SELECT  ('Product/' + folder + '/' + name) AS DM, datepart(hour,ForecastDate) as hour FROM   " + entity.TableName + strWhere + "ORDER BY ForecastDate,Period DESC";
                dt = m_Database.GetDataTable(strSQL);
                sb.Append("'addBut':\"");
                sb.Append("<select id='selectHour' onchange='selectChange()'>");
                foreach (DataRow rows in dt.Rows)
                {
                    if (rows[1].ToString() == hour.ToString())
                        sb.AppendFormat("<option value ='{0}' selected='true'>{0}</option>", rows[1]);
                    else
                        sb.AppendFormat("<option value ='{0}' >{0}</option>", rows[1]);
                }
                sb.Append("</select >");
                sb.Append("\"");
            }
            sb.Append("}");
            string jsonStr = sb.ToString();
            return jsonStr;
        }
        public string CreateBottomSelect(string Datetime, string entityName, string json, string type, string Period)
        {
            string[] info = json.Substring(1, json.Length - 2).Split(new char[] { ';', ':' });
            StringBuilder sb = new StringBuilder("{");
            DataTable dt = new DataTable();
            int hour=DateTime.Parse(Datetime).Hour;
            string startTime = DateTime.Parse(Datetime).ToString("yyyy-MM-dd 00:00:00");
            string endTime = DateTime.Parse(Datetime).ToString("yyyy-MM-dd 23:59:59");
            Entity entity = new Entity(m_Database, entityName);
            string strWhere = "  WHERE ";
            if (entity.Condition != "")
                strWhere = strWhere + entity.Condition + " AND ";//+ " AND " 
            if (info[7] != "")
                strWhere = strWhere + " Type='" + type + "'  AND ";
            if (info[9] == "")
            {
                strWhere =strWhere+ "   ForecastDate BETWEEN'" + startTime + "' AND '" + endTime + "'";
                string strSQL = "SELECT  ('Product/' + folder + '/' + name) AS DM, datepart(hour,ForecastDate) as hour FROM   " + entity.TableName + strWhere + "ORDER BY ForecastDate,Period DESC";
                dt = m_Database.GetDataTable(strSQL);
                sb.Append("'addBut':\"");
                sb.Append("<select id='selectHour' onchange='selectChange()'>");
                foreach (DataRow rows in dt.Rows)
                {
                    if (rows[1].ToString() == hour.ToString())
                        sb.AppendFormat("<option value ='{0}' selected='true'>{0}</option>", rows[1]);
                    else
                        sb.AppendFormat("<option value ='{0}' >{0}</option>", rows[1]);
                }
                sb.Append("</select >");
                sb.Append("\"");
            }
            else
            {
                strWhere = strWhere+ "   ForecastDate ='" + DateTime.Parse(Datetime).ToString("yyyy-MM-dd HH:00:00") + "'";
                string strSQL = "SELECT  ('Product/' + folder + '/' + name) AS DM, Period as hour FROM   " + entity.TableName + strWhere + "ORDER BY Period ";
                dt = m_Database.GetDataTable(strSQL);
                sb.Append("'addBut':\"");
                sb.Append("<select id='selectHour' onchange='selectChange()'>");
                foreach (DataRow rows in dt.Rows)
                {
                    if (rows[1].ToString() == Period)
                        sb.AppendFormat("<option value ='{0}' selected='true'>{0}</option>", rows[1]);
                    else
                        sb.AppendFormat("<option value ='{0}' >{0}</option>", rows[1]);
                }
                sb.Append("</select >");
                sb.Append("\",");
                sb.Append("'period':\"");
                sb.Append("<select id='selectperiod'>");
                string[] periodArray = info[9].Split(',');
                for (int i = 0; i < periodArray.Length; i++)
                {
                    if (periodArray[i] == DateTime.Parse(Datetime).Hour.ToString())
                        sb.AppendFormat("<option value ='{0}' selected='true'>{0}</option>", periodArray[i]);
                    else
                        sb.AppendFormat("<option value ='{0}' >{0}</option>", periodArray[i]);
                }
                sb.Append("</select >");
                sb.Append("\"");
            }
            sb.Append("}");
            string jsonStr = sb.ToString();
            return jsonStr;
        }
        public string ReduceButton(string entityName, string dateTime, string hour, string json, string type, string period)
        {
            string[] info = json.Substring(1, json.Length - 2).Split(new char[] { ';', ':' });
            StringBuilder sb = new StringBuilder("{");
            DataTable dt = new DataTable();
            int hours = 0;
            string forecastDate = "";
            string strWhere = " WHERE ";
            forecastDate = DateTime.Parse(dateTime).AddHours(hours).ToString("yyyy-MM-dd HH:00:00");
            Entity entity = new Entity(m_Database, entityName);
            string strSQL="";

            if (entity.Condition != "")
                strWhere = strWhere+entity.Condition + "  AND ";//+ " AND "      
            if (info[7] != "")
            {
                if (period == "airQuality")
                    strWhere = strWhere + "  Station='" + info[7] + "'  AND";
                else 
                    strWhere = strWhere + "  Type='" + type + "'  AND";
            }
            if (hour != "-1"&&hour != "-2")//-1��ʾʱ����ǰ��ѯ��-2��ʾʱ�������ѯ
            {
                if (info[9] == "")
                {
                    hours = int.Parse(hour);
                    forecastDate = DateTime.Parse(dateTime).AddHours(hours).ToString("yyyy-MM-dd HH:00:00");
                    strWhere = strWhere + "   ForecastDate= '" + forecastDate + "'";
                }
                else
                {
                    hours = int.Parse(period);
                    forecastDate = DateTime.Parse(dateTime).AddHours(hours).ToString("yyyy-MM-dd HH:00:00");
                    strWhere = strWhere + "   ForecastDate= '" + forecastDate + "' AND Period='"+hour+"'";
                }
                strSQL = "SELECT  ('Product/' + folder + '/' + name) AS DM, datepart(hour,ForecastDate) as hour FROM   " + entity.TableName + strWhere + "ORDER BY ForecastDate,Period DESC";
               
            }
            else
            {
                
                string str = "";
                if (info[9] == "")
                {
                    if (hour == "-1")
                    {
                        str = "   ForecastDate <'" + forecastDate + "'";
                        strSQL = "SELECT  TOP(1) ForecastDate  FROM   " + entity.TableName + strWhere + str + " ORDER BY ForecastDate DESC";
                    }
                    else
                    {
                        forecastDate = DateTime.Parse(dateTime).AddDays(1).ToString("yyyy-MM-dd HH:00:00");
                        str = "   ForecastDate >='" + forecastDate + "'";
                        strSQL = "SELECT  TOP(1) ForecastDate  FROM   " + entity.TableName + strWhere + str + " ORDER BY ForecastDate ";
                    }
                    forecastDate = m_Database.GetFirstValue(strSQL);
                    if (forecastDate == "")
                        return "";
                    string startTime = DateTime.Parse(forecastDate).ToString("yyyy-MM-dd 00:00:00");
                    string endTime = DateTime.Parse(forecastDate).ToString("yyyy-MM-dd 23:59:59");
                    strWhere = strWhere + "    ForecastDate BETWEEN'" + startTime + "' AND '" + endTime + "'";
                    strSQL = "SELECT  ('Product/' + folder + '/' + name) AS DM, datepart(hour,ForecastDate) as hour FROM   " + entity.TableName + strWhere + "ORDER BY ForecastDate,Period DESC";
                }
                else
                {
                    hours = int.Parse(period);
                    if (hour == "-1")
                        forecastDate = DateTime.Parse(dateTime).AddDays(-1).AddHours(hours).ToString("yyyy-MM-dd HH:00:00");
                    else
                        forecastDate = DateTime.Parse(dateTime).AddDays(1).AddHours(hours).ToString("yyyy-MM-dd HH:00:00");
                    strWhere = strWhere + "    ForecastDate ='" + forecastDate + "' ";
                    strSQL = "SELECT  ('Product/' + folder + '/' + name) AS DM, Period FROM   " + entity.TableName + strWhere + " ORDER BY Period ";
                }
            }
           
            dt = m_Database.GetDataTable(strSQL);
            if (dt.Rows.Count <= 0)
                return "";
            string src = dt.Rows[0][0].ToString();
            if (hour == "-1" || hour == "-2")
            {
                sb.Append("'addBut':\"");
                sb.Append("<select id='selectHour' onchange='selectChange()'>");
                foreach (DataRow rows in dt.Rows)
                {
                    string selectValue = "";
                    if (info[9] == "")
                        selectValue = DateTime.Parse(forecastDate).Hour.ToString();
                    else
                    {
                        if (hour == "-1")
                            selectValue = dt.Rows[dt.Rows.Count - 1][1].ToString();
                        else
                            selectValue = dt.Rows[0][1].ToString();
                    }
                    if (rows[1].ToString() == selectValue)
                    {
                        sb.AppendFormat("<option value ='{0}' selected='true'>{0}</option>", rows[1]);
                        src = rows[0].ToString();
                    }
                    else
                        sb.AppendFormat("<option value ='{0}' >{0}</option>", rows[1]);
                }
                sb.Append("</select >");
                sb.Append("\",");
                sb.AppendFormat("'time':'{0}',",DateTime.Parse(forecastDate).ToString("yyyy-MM-dd"));

            }
            sb.AppendFormat("'src':'{0}'",src);
            sb.Append("}");
            string jsonStr = sb.ToString();
            return jsonStr;
        }
        public DataTable QueryListAirQuality(string entityName, string entityObj,string station)
        {
            Entity entity = new Entity(m_Database, entityName);
            string strWhere = " WHERE ";
            if (entityObj != "")
                strWhere = strWhere + GetUserWhere(entity, entityObj);
            else
                strWhere = strWhere + GetDefaultWhere(entity);

            if (entity.Condition != "")
            {
                strWhere = strWhere + " AND " + entity.Condition;
            }
            if(station!="")
                strWhere = strWhere + " AND  station='" + station+"'";
            string strSQL = "SELECT ('Product/' + folder + '/' + name) AS DM, (CASE WHEN Period IS NULL THEN CONVERT(varchar(16),ForecastDate, 120) ELSE SUBSTRING(CONVERT(varchar(16),ForecastDate, 120), 0, 5)+'��'+SUBSTRING(CONVERT(varchar(16), ForecastDate, 120), 6, 2) + '��' + SUBSTRING(CONVERT(varchar(16), ForecastDate, 120), 9, 2) + '��' + SUBSTRING(CONVERT(varchar(16), ForecastDate, 120), 12, 2) + 'ʱ F' + Period + 'H' END) AS MC,TYPE,Period FROM  " + entity.TableName + strWhere + "ORDER BY NAME";

            try
            {
                DataSet dt = m_Database.GetDataset(strSQL);
                if (dt.Tables.Count > 0)
                {
                    DataTable dTable = dt.Tables[0];
                    if (dTable.Rows[0]["MC"].ToString().IndexOf('F') > 0)
                    {
                        foreach (DataRow dr in dTable.Rows)
                        {
                            string oldStr = dr[1].ToString().Substring(0, 14);
                            int hourAdd = int.Parse(dr[1].ToString().Substring(16, 3));
                            string newStr = DateTime.Parse(oldStr).AddHours(hourAdd).ToString("yyyy-MM-dd HH:00");
                            dr[1] = newStr;
                        }
                    }
                    return dTable;
                }
                else
                    return null;
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        public DataTable QueryList(string entityName, string entityObj)
        {
            Entity entity = new Entity(m_Database, entityName);
            string strWhere = " WHERE ";
            if (entityObj != "")
                strWhere = strWhere + GetUserWhere(entity, entityObj);
            else
                strWhere = strWhere + GetDefaultWhere(entity);

            if (entity.Condition != "")
            {
                strWhere = strWhere + " AND " + entity.Condition;
            }
            string strSQL = "SELECT ('Product/' + folder + '/' + name) AS DM, (CASE WHEN Period IS NULL THEN CONVERT(varchar(16),ForecastDate, 120) ELSE SUBSTRING(CONVERT(varchar(16),ForecastDate, 120), 0, 5)+'��'+SUBSTRING(CONVERT(varchar(16), ForecastDate, 120), 6, 2) + '��' + SUBSTRING(CONVERT(varchar(16), ForecastDate, 120), 9, 2) + '��' + SUBSTRING(CONVERT(varchar(16), ForecastDate, 120), 12, 2) + 'ʱ F' + Period + 'H' END) AS MC,TYPE,Period FROM  " + entity.TableName + strWhere + "ORDER BY NAME";
            if (entityName == "HuadongMeto" || entityName == "HuadongForecast" || entityName == "WeekForecast" || entityName == "ChangForecast" || entityName == "ShanghaiAna")
            {
                strSQL = "SELECT ('Product/' + folder + '/' + name) AS DM, (CASE WHEN Period IS NULL THEN CONVERT(varchar(16),ForecastDate, 120) ELSE SUBSTRING(CONVERT(varchar(16),ForecastDate, 120), 0, 5)+'��'+SUBSTRING(CONVERT(varchar(16), ForecastDate, 120), 6, 2) + '��' + SUBSTRING(CONVERT(varchar(16), ForecastDate, 120), 9, 2) + '��' + SUBSTRING(CONVERT(varchar(16), ForecastDate, 120), 12, 2) + 'ʱ F' + Period + 'H' END) AS MC,TYPE,Period FROM  " + entity.TableName + strWhere + "ORDER BY ForecastDate";
            }
            try
            {
                DataSet dt = m_Database.GetDataset(strSQL);
                if (dt.Tables.Count > 0)
                {
                    DataTable dTable = dt.Tables[0];
                    if (dTable.Rows[0]["MC"].ToString().IndexOf('F') > 0)
                    {
                        foreach (DataRow dr in dTable.Rows)
                        {
                            string oldStr = dr[1].ToString().Substring(0, 14);
                            int hourAdd = int.Parse(dr[1].ToString().Substring(16, 3));
                            //string newStr = DateTime.Parse(oldStr).AddHours(hourAdd).ToString("yyyy-MM-dd HH:00");
                            string newStr = null; ;
                            if (entityName == "HuadongMeto" || entityName == "HuadongForecast" || entityName == "WeekForecast" || entityName == "ChangForecast" || entityName == "ShanghaiAna") 
                            {
                                newStr = DateTime.Parse(oldStr).AddHours(hourAdd).ToString("yyyy-MM-dd");
                            }
                            else
                            {
                                newStr = DateTime.Parse(oldStr).AddHours(hourAdd).ToString("yyyy-MM-dd HH:00");
                            }
                            dr[1] = newStr;
                        }
                    }
                    return dTable;
                }
                else
                    return null;
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        /// <summary>
        /// ��ȡȱʡ��Where���
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private string GetDefaultWhere(Entity entity)
        {
            IList fieldsProperty = entity.GetProperties(EntityStateContants.esQuery, QueryTypeContants.qtIndexQuery);
            PropertyOV fieldValue = null;
            FilterOV filterOV = new FilterOV();
            string strPeriod = GetPeriod(entity.Name);

            string strSQL = "";
            for (int i = 0; i < fieldsProperty.Count; i++)
            {
                fieldValue = (PropertyOV)fieldsProperty[i];
                if (fieldValue.IsDictionary)
                {
                    strSQL = "";
                    if (fieldValue.YField != "")
                    {
                        PropertyOV dpField = entity.GetPropertyOV(fieldValue.YField);
                        strSQL = "SELECT TOP 1 DM FROM " + dpField.DictName;
                        strSQL = " WHERE DP LIKE '%" + m_Database.GetFirstValue(strSQL) + "%'";

                    }
                    strSQL = "SELECT TOP 1 MC FROM " + fieldValue.DictName + strSQL;
                    fieldValue.ShowValue = m_Database.GetFirstValue(strSQL);
                }
                else if (fieldValue.FieldType == FieldTypeContants.ET_DATETIME)
                {
                    string strLastestDatetime = GetLastestDatetime(entity);
                    if (strLastestDatetime != "")
                    {
                        DateTime dtNow = DateTime.Parse(strLastestDatetime);
                        if (strPeriod == "")
                            fieldValue.ShowValue = dtNow.ToString("yyyy-MM-dd 00:00:00") + "||" + dtNow.ToString("yyyy-MM-dd 23:59:59");
                        else
                        {
                            //Ԥ������
                            //string[] periods = strPeriod.Split(',');
                            fieldValue.ShowValue = strLastestDatetime;
                        }
                    }
                }
                filterOV.Add(fieldValue);
            }
            return entity.BuildQuerySQL(filterOV);
        }

        /// <summary>
        /// ��ȡ�û����������
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private string GetUserWhere(Entity entity, string entityObj)
        {
            IList<PropertyJsOV> properties;
            PropertyJsOV fieldJsOV;
            PropertyOV fieldValue;
            FilterOV filterOV = new FilterOV();
            string strSQL = "";
            string strPeriodSQL = "";
            string strForecastSQL = "";

            properties = JsonConvert.DeserializeObject<IList<PropertyJsOV>>(entityObj);
            for (int i = 0; i < properties.Count; i++)
            {
                fieldJsOV = (PropertyJsOV)properties[i];
                fieldValue = entity.GetPropertyOV(fieldJsOV.Name);
                if (fieldValue.IsDictionary && fieldValue.IsEvent)
                {
                    string[] showValues = fieldJsOV.ShowValue.Split('+');//�˴����⴦��   �����������  ��ΰ��   2013-05-06
                    fieldValue.ShowValue = showValues[0];
                    strPeriodSQL = " AND PERIOD = '000'";
                    if (showValues.Length > 1)
                    {
                        strForecastSQL = "PERIOD <= '" + showValues[1] + "' AND " + showValues[2];
                    }
                }
                else
                {
                    fieldValue.ShowValue = fieldJsOV.ShowValue;
                }
                filterOV.Add(fieldValue);
            }

            strSQL = entity.BuildQuerySQL(filterOV);

            //�Ի����������⴦��
            string strSplit = " AND ";
            int andIndex = strSQL.LastIndexOf(strSplit);
            if (andIndex > 0)
            {
                strSplit = strSQL.Substring(andIndex);
                if (strForecastSQL != "")
                    strSQL = "(" + strSQL + strPeriodSQL + ") OR (" + strForecastSQL + strSplit + ")";
                else
                    strSQL = strSQL + strPeriodSQL;
            }


            return strSQL;
        }

        /// <summary>
        /// ��ȡ��Ʒ��Ԥ��ʱЧ���ж���Ԥ�����ݻ���ʵʱ����
        /// </summary>
        /// <param name="entityName"></param>
        /// <returns></returns>
        private string GetPeriod(string entityName)
        {
            string strSQL = "SELECT PERIOD FROM T_ImageProduct WHERE ENTITYNAME = '" + entityName + "'";
            return m_Database.GetFirstValue(strSQL);
        }
        /// <summary>
        /// ������ת��ΪJSON����
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public string ToJSON(object obj)
        {
            string josnData = string.Empty; 
            if (obj == null)
            {
                return "";
            }
            //���ΪDataTable����
            if (obj.GetType() == typeof(System.Data.DataTable))
            {
                DataTable dTable = (DataTable)obj;
                josnData = JsonConvert.SerializeObject(obj, new DataTableConverter());
                //JsonConvert.DeserializeObject(josnData,new DataTableConverter());
                //��Ҫ�����ܵļ�¼���Ļ�
                if (dTable.TableName.Contains("data:{0}"))
                {
                    josnData = "{" + string.Format(dTable.TableName, josnData) + "}";
                }
                return josnData;
            }
            else if (obj.GetType() == typeof(string))
            {
                return obj.ToString();
            }
            else if (obj.GetType() == typeof(DataSet))
            {
                josnData = JsonConvert.SerializeObject(obj, new DataTableConverter());
                return josnData;
            }
            return JsonConvert.SerializeObject(obj);
        }


        //2015��11��9�գ��ƺ���

        /// <summary>
        /// ��ѯ�������AQI��ʱ��Ԥ�������ݱ�
        /// </summary>
        /// <param name="tableName">��ѯ������</param>
        /// <param name="curDate">��ǰ����</param>
        /// <param name="siteID">����վ�㣨��һ㣨58367����ʾ�Ϻ���</param>
        /// <returns></returns>
        public string GetAQIForecast(string tableName,string curDate,string siteID)
        {
           //string strSQL= "select * from dbo.T_ForecastSite where  ForecastDate=(select max(ForecastDate) from dbo.T_ForecastSite)"
            string strSQL = "select * from T_ForecastSite where  ForecastDate=(select max(ForecastDate) from T_ForecastSite)";
            DataSet dt = m_Database.GetDataset(strSQL);
                if (dt.Tables.Count > 0)
                {
                    DataTable dTable = dt.Tables[0];
                    
                }
            return "query Test";
        }
    }

}