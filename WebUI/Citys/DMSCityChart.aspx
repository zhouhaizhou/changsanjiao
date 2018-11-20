﻿<%@ Page Language="C#" AutoEventWireup="true" CodeFile="DMSCityChart.aspx.cs" Inherits="DMSCityChart" %>
<%@ Register Src="~/Citys/WebUserControlII.ascx"TagName="WebUserControl"TagPrefix="uc1" %> 

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head runat="server">
    <title>多模式单城市预报</title>
    <link href="css/css.css?v=201610210" rel="stylesheet" type="text/css" />
    <link type="text/css" rel="stylesheet" href="../Ext/resources/css/ext-all.css"/>
      <link rel="stylesheet" type="text/css" href="css/jquery.citypicker.css"/>
    <script type="text/javascript" src="../Ext/adapter/ext/ext-base.js"></script>
    <script type="text/javascript" src="../Ext/ext-all.js"></script>
    <script type="text/javascript" src="../Ext/ext-lang-zh_CN.js"></script>
    <script language="javascript" type="text/javascript" src="js/DMSCityChart.js?v=201610241111"></script>
    <script language="javascript" type="text/javascript" src="../JS/Utility.js"></script>
    <script language ="javascript" type="text/javascript" src="../JS/highlight-active-input.js"> </script>
    <script language="javascript" type="text/javascript" src="../DatePicker/WdatePicker.js"></script>
    <!-- stockjs --> 
    <script type="text/javascript" src="../JS/Chart/jquery.min.js"></script>
    <script type="text/javascript" src="../JS/Chart/highstock.js"></script>
    <script type="text/javascript" src="JS/gridLine.js"></script>
        <script type="text/javascript" src="../JS/Chart/modules/exporting.js"></script>
    <script language="javascript" type="text/javascript">
        var lastTab = "<%=m_FirstTab %>";//当前选中的污染物
        var station="<%=m_Station %>"
    </script>
</head>
<body>
    <div id="tabbtn">
       <ul id="tabItem" runat="server">
       </ul>
    </div>
    <div id="contentNone">
        <div id="tool">
            <div class="checkStyle">
                 <div class="checkLable">起报时间：</div>
                 <input id="H00" type="text" class="selectDateFormStyle" value="<%= m_FromDate%>"  onclick="WdatePicker({dateFmt:'yyyy年MM月dd日',maxDate:'#F{$dp.$D(\'H01\')}'})"/>
            </div>
            <div class="checkStyleII">
                 <div class="checkLable" >起报时次：20</div>
                 <input id="H01"style=" display:none"  type="text" class="selectDateFormStyle" value="<%= m_ToDate%>" onclick="WdatePicker({dateFmt:'yyyy年MM月dd日',minDate:'#F{$dp.$D(\'H00\')}'})"/>
                 &nbsp;&nbsp
              
                     <input type="text" id="city" style=" width:120px; height:20px; display:none;" value="上海-上海市"/>
               <div style=" margin-top:-6px;">
                 &nbsp;&nbsp 分辨率：
                 <input id="hour" type="radio" class="radioBtn" name="resolution" checked="checked" onclick="ChangleType(this)" style="vertical-align: -1px;"/>
<label for="hour">小时</label>
<input id="day" class="radioBtn" type="radio" name="resolution" onclick="ChangleT`ype(this)" style="vertical-align: -1px;"/>
<label for="day">日</label></div>
            </div>
            <div id="tool_btn_area">
            <button type="button" class="normal-btn input-btnQuery"  id="btnQuery"  onclick="clickQuery()" onmouseover="this.className='normal-btn-h input-btnQuery'" onmouseout="this.className='normal-btn input-btnQuery'">
            <span class="select-Query"></span>
            <span class="select-text">查询</span>
             </button>
            <%--<input type="button" id="btnQuery"  onclick="doQueryChart()" class="query defaultQueryButton"  onmouseover="this.className = 'query overQueryButton';" onmouseout ="this.className ='query defaultQueryButton';"   />--%>
         </div>
        </div>
        <div class="checkStyle" style=" width:1056px; margin-top:10px;  display:none ">
                     <div class="checkLable" style="width:75px">重点城市：</div>
                      <div id="c1" class="radioChecked"><a href="javascript:radioClickModule('c1','上海市');">上海市</a></div>
                      <div id="c2" class="radioUnChecked"><a href="javascript:radioClickModule('c2','济南市');">济南市</a></div>
                      <div id="c3" class="radioUnChecked"><a href="javascript:radioClickModule('c3','青岛市');">青岛市</a></div>
                      <div id="c4" class="radioUnChecked"><a href="javascript:radioClickModule('c4','合肥市');">合肥市</a></div>
                      <div id="c5" class="radioUnChecked"><a href="javascript:radioClickModule('c5','南京市');">南京市</a></div>
                      <div id="c6" class="radioUnChecked"><a href="javascript:radioClickModule('c6','苏州市');">苏州市</a></div>
                      <div id="c7" class="radioUnChecked"><a href="javascript:radioClickModule('c7','杭州市');">杭州市</a></div>
                      <div id="c8" class="radioUnChecked"><a href="javascript:radioClickModule('c8','宁波市');">宁波市</a></div>
                      <div id="c9" class="radioUnChecked"><a href="javascript:radioClickModule('c9','南昌市');">南昌市</a></div>
                      <div id="c10" class="radioUnChecked"><a href="javascript:radioClickModule('c10','福州市');">福州市</a></div>
                      <div id="c11" class="radioUnChecked"><a href="javascript:radioClickModule('c11','厦门市');">厦门市</a></div>
             
      </div>
              <table align="center" cellpadding="0" cellspacing="0" class="hct">
                <tr>
                    <td>
                       <div id="container1" style="width: 1000px; height: 350px; margin:0 0 0 0;"></div></td>
                </tr>
                <tr>
                    <td>
                       <div id="container2" style="width: 1000px; height:350px; margin: 0 0 0 0 0;"></div></td>
                </tr>
                   <tr>
                    <td>
                       <div id="container3" style="width: 1000px; height:350px; margin: 0 0 0 0 0;"></div></td>
                </tr>
                   <tr>
                    <td>
                       <div id="container4" style="width: 1000px; height:350px; margin: 0 0 0 0 0;"></div></td>
                </tr>
                      <tr>
                    <td>
                       <div id="container5" style="width: 1000px; height:350px; margin: 0 0 0 0 0;"></div></td>
                </tr>
                      <tr>
                    <td>
                       <div id="container6" style="width: 1000px; height:350px; margin: 0 0 0 0 0;"></div></td>
                </tr>
            </table>
         
    </div>
       <div style=" height:200px; position: relative;float: right;color: black; width: 220px; margin-right: 20px; margin-top: -2080px;  z-index: 9999;">
            <uc1:WebUserControl ID="WebUserControl1" runat="server"  />  
       </div>
</body>
</html>