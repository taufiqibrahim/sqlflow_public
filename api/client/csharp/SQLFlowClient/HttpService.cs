﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Diagnostics;

namespace SQLFlowClient
{
    public static class HttpService
    {
        public static async Task Request(Options options)
        {
            var stopWatch = Stopwatch.StartNew();
            var config = new Config
            {
                Host = "https://api.gudusoft.com",
                Token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJhdWQiOiJndWVzdFVzZXIiLCJleHAiOjE1ODEyMDY0MDAsImlhdCI6MTU3MzQzMDQwMH0.-lvxaPlXmHbtgSFgW7ycu8KUczRiFZy5A1aNRGY-tKM"
            };
            try
            {
                if (File.Exists("./config.json"))
                {
                    var json = JObject.Parse(File.ReadAllText("./config.json"));
                    if (json["Host"] != null && json["Host"].ToString() != "")
                    {
                        config.Host = json["Host"].ToString();
                    }
                    if (json["Token"] != null && json["Token"].ToString() != "")
                    {
                        config.Token = json["Token"].ToString();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Invalid config.json :\n{e.Message}");
                return;
            }
            StreamContent sqlfile;
            try
            {
                string path = Path.GetFullPath(options.SQLFile);
                sqlfile = new StreamContent(File.Open(options.SQLFile, FileMode.Open));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Open file failed.\n{e.Message}");
                return;
            }
            var types = options.ShowRelationType.Split(",")
                .Where(p => Enum.GetNames(typeof(RelationType)).FirstOrDefault(t => t.ToLower() == p.ToLower()) == null)
                .ToList();
            if (types.Count != 0)
            {
                Console.WriteLine($"Wrong relation type : { string.Join(",", types) }.\nIt should be one or more from the following list : fdd, fdr, frd, fddi, join");
                return;
            }
            string dbvendor = Enum.GetNames(typeof(DBVendor)).FirstOrDefault(p => p.ToLower() == options.DBVendor.ToLower());
            if (dbvendor == null)
            {
                Console.WriteLine($"Wrong database vendor : {options.DBVendor}.\nIt should be one of the following list : " +
                    $"bigquery, couchbase, db2, greenplum, hana , hive, impala , informix, mdx, mysql, netezza, openedge," +
                    $" oracle, postgresql, redshift, snowflake, mssql, sybase, teradata, vertica");
                return;
            }
            var form = new MultipartFormDataContent{
                { sqlfile                                                , "sqlfile"           , "sqlfile" },
                { new StringContent("dbv"+dbvendor)                      , "dbvendor"         },
                { new StringContent(options.ShowRelationType)            , "showRelationType" },
                { new StringContent(options.SimpleOutput.ToString())     , "simpleOutput"     },
                { new StringContent(options.IgnoreRecordSet.ToString())  , "ignoreRecordSet"  },
            };
            try
            {
                string url = $"{config.Host}/gspLive_backend/sqlflow/generation/sqlflow/" + (options.IsGraph ? "graph" : "");
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", config.Token);
                using var response = await client.PostAsync(url, form);
                if (response.IsSuccessStatusCode)
                {
                    stopWatch.Stop();
                    var text = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(text);
                    var data = json["data"]?.ToString();
                    var dbobjs = json.SelectToken("data.dbobjs");
                    var sqlflow = json.SelectToken("data.sqlflow");
                    var graph = json.SelectToken("data.graph");
                    if (data != null && dbobjs != null || data != null && sqlflow != null && graph != null)
                    {
                        if (options.Output != "")
                        {
                            try
                            {
                                File.WriteAllText(Path.GetFullPath(options.Output), data);
                                Console.WriteLine($"Output has been saved to {options.Output}.");
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Save File failed.{e.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine(data ?? "");
                        }
                    }
                    if (json["error"]?.ToString() != null)
                    {
                        Console.WriteLine($"Success with some errors.Executed in {stopWatch.Elapsed.TotalSeconds.ToString("0.00")} seconds by host {config.Host}.");
                    }
                    else
                    {
                        Console.WriteLine($"Success.Executed in {stopWatch.Elapsed.TotalSeconds.ToString("0.00")} seconds by host {config.Host}.");
                    }
                }
                else
                {
                    Console.WriteLine($"Wrong response code {(int)response.StatusCode} {response.StatusCode}.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"An unknonwn exeception occurs :\n{e.Message}");
            }
        }
    }
}
