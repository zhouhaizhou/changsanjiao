﻿<%@ WebHandler Language="C#" Class="ChangjiangImgDownload" %>

using System;
using System.Web;

public class ChangjiangImgDownload : IHttpHandler {
    
    public void ProcessRequest (HttpContext context) {
        if (context.Request.QueryString["ImgPath"] != null)
        {
            string siteFileUrl = context.Request.QueryString["ImgPath"];
            if (System.IO.File.Exists(siteFileUrl))
            {
                System.IO.FileInfo fi = new System.IO.FileInfo(siteFileUrl);
                if (fi.Exists)
                {
                    context.Response.Clear();
                    context.Response.AddHeader("Content-Disposition", "attachment; filename=" + context.Server.UrlEncode(fi.Name));
                    context.Response.ContentType = "application/x-download";
                    context.Response.Filter.Close();
                    context.Response.WriteFile(fi.FullName);
                    context.Response.End();
                }
                else
                {
                    context.Response.Status = "404 File Not Found";
                    context.Response.StatusCode = 404;
                    context.Response.StatusDescription = "File Not Found";
                    context.Response.Write("File Not Found");
                    context.Response.End();
                }
            }
        }
    }
 
    public bool IsReusable {
        get {
            return false;
        }
    }

}