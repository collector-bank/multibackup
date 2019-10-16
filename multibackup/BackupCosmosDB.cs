﻿using Destructurama.Attributed;
using Serilog;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace multibackup
{
    public class BackupCosmosDB : BackupJob
    {
        [NotLogged]
        public string ConnectionString { get; set; }
        [NotLogged]
        public string Collection { get; set; }

        protected override void Export(string backupfile)
        {
            string dtbinary = Tools.DtBinary;

            for (int tries = 1; tries <= 5; tries++)
            {
                using (new ContextLogger(new Dictionary<string, object>() { ["Tries"] = tries }))
                {
                    Log.Information("Exporting: {Backupfile}", backupfile);

                    Stopwatch watch = Stopwatch.StartNew();

                    string appfolder = Path.GetDirectoryName(Path.GetDirectoryName(dtbinary));
                    string logfile = GetLogFileName(appfolder, Name);

                    string args = $"/ErrorLog:{logfile} /ErrorDetails:All /s:DocumentDB /s.ConnectionString:{ConnectionString} /s.Collection:{Collection} /t:JsonFile /t.File:{backupfile} /t.Prettify";

                    int result = RunCommand(dtbinary, args);
                    watch.Stop();
                    Statistics.ExportCosmosDBTime += watch.Elapsed;
                    long elapsedms = (long)watch.Elapsed.TotalMilliseconds;

                    if (new FileInfo(logfile).Length > 0)
                    {
                        Log.Information("Reading logfile: {Logfile}", logfile);
                        string[] rows = File.ReadAllLines(logfile);
                        Log.ForContext("LogfileContent", LogHelper.TruncateLogFileContent(rows)).Information("dt results");
                    }

                    Log.Information("Deleting logfile: {Logfile}", logfile);
                    File.Delete(logfile);

                    if (result == 0 && File.Exists(backupfile) && new FileInfo(backupfile).Length > 0)
                    {
                        long size = new FileInfo(backupfile).Length;
                        long sizemb = size / 1024 / 1024;
                        Statistics.UncompressedSize += size;
                        Log
                            .ForContext("ElapsedMS", elapsedms)
                            .ForContext("Backupfile", backupfile)
                            .ForContext("Size", size)
                            .ForContext("SizeMB", sizemb)
                            .Information("Export success");
                        return;
                    }
                    else
                    {
                        Log
                            .ForContext("Binary", dtbinary)
                            .ForContext("Commandargs", LogHelper.Mask(args, new[] { ConnectionString, Collection }))
                            .ForContext("Result", result)
                            .ForContext("ElapsedMS", elapsedms)
                            .ForContext("Backupfile", backupfile)
                            .Warning("Export fail");
                    }
                }
            }

            if (File.Exists(backupfile) && new FileInfo(backupfile).Length == 0)
            {
                Log.Information("Deleting empty file: {Backupfile}", backupfile);
                File.Delete(backupfile);
            }

            Log.Warning("Couldn't export database to file: {Backupfile}", backupfile);
        }
    }
}
