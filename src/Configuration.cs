/*
    Licensed to the Apache Software Foundation (ASF) under one
    or more contributor license agreements.  See the NOTICE file
    distributed with this work for additional information
    regarding copyright ownership.  The ASF licenses this file
    to you under the Apache License, Version 2.0 (the
    "License"); you may not use this file except in compliance
    with the License.  You may obtain a copy of the License at

      http://www.apache.org/licenses/LICENSE-2.0

    Unless required by applicable law or agreed to in writing,
    software distributed under the License is distributed on an
    "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
    KIND, either express or implied.  See the License for the
    specific language governing permissions and limitations
    under the License.  
*/
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Threading;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public class TemperaturePoint
{
    public int Temperature { get; set; }
    public int Load { get; set; }

    public TemperaturePoint()
    {
        Temperature = 0;
        Load = 0;
    }

    public TemperaturePoint(int temperature, int load)
    {
        Temperature = temperature;
        Load = load;
    }
}

public class Configuration
{
    public bool AutoFanControl { get; set; } = false;
    public string LogLevel { get; set; } = "Info";
    public Dictionary<string, List<TemperaturePoint>> Sensors = new Dictionary<string, List<TemperaturePoint>>();
    public List<TemperaturePoint> Default { get; set; } = new List<TemperaturePoint>();
}

public class ConfigurationManager
{
    public const string CONFIGURATION_FILE = "config.yaml";
    public const int RETRY_MILLISECONDS = 500;
    public const int RETRY_TIMES = 3;
    public const string ELEGIBLE_GROUP = "Authenticated Users";
    private object m_mutex;
    private IDeserializer m_deserializer;
    private ISerializer m_serializer;
    private string m_filename;
    private Configuration m_configuration;
    public Configuration Configuration {
        get {
            lock (m_mutex) {
                return m_configuration;
            }
        }
        set {
            lock (m_mutex) {
                m_configuration = value;
            }
        }
    }
    public delegate void ConfiguragionEventHandler(object sender, Configuration configuration);
    public event ConfiguragionEventHandler Changed;

    public ConfigurationManager()
    {
        m_mutex = new object();
        m_deserializer = new DeserializerBuilder()
          .WithNamingConvention(CamelCaseNamingConvention.Instance)
          .Build();
        m_serializer = new SerializerBuilder()
          .WithNamingConvention(CamelCaseNamingConvention.Instance)
          .Build();
        m_configuration = new Configuration();
    }

    public void Init()
    {
        string path = Path.GetDirectoryName(new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath);
        m_filename = path + @"\" + CONFIGURATION_FILE;
        SettingConfigFilePermissions(m_filename);
        CreateFileWatcher(path);
        OnChanged(this, new FileSystemEventArgs(WatcherChangeTypes.Changed, path, CONFIGURATION_FILE));
    }

    private void SettingConfigFilePermissions(string fileName)
    {
        if (File.Exists(fileName)) {
            FileInfo fileInfo = new FileInfo(fileName);
            FileSecurity fileSecurity = fileInfo.GetAccessControl();
            fileSecurity.AddAccessRule(new FileSystemAccessRule(ELEGIBLE_GROUP, FileSystemRights.FullControl, AccessControlType.Allow));
            fileInfo.SetAccessControl(fileSecurity);
        }
    }

    private string ReadConfigFile()
    {
        if (File.Exists(m_filename)) {
            return File.ReadAllText(m_filename);
        }
        return "";
    }

    private void WriteConfigFile(string yml)
    {
        File.WriteAllText(m_filename, yml);
    }

    private void Load()
    {
        string yml = Utils.InvokeAndRetryOnException<IOException, string>(
          new Utils.Action<string>(ReadConfigFile),
          RETRY_TIMES,
          TimeSpan.FromMilliseconds(RETRY_MILLISECONDS)
        );
        lock (m_mutex) {
            Configuration = m_deserializer.Deserialize<Configuration>(yml);
            if (Configuration.Default.Count == 0) {
                Configuration.Default.Add(new TemperaturePoint(30, 20));
                Configuration.Default.Add(new TemperaturePoint(50, 50));
                Configuration.Default.Add(new TemperaturePoint(60, 100));
            }
            foreach (string key in Configuration.Sensors.Keys) {
                Configuration.Sensors[key].Sort((a, b) => a.Temperature - b.Temperature);
            }
            Configuration.Default.Sort((a, b) => a.Temperature - b.Temperature);
        }
    }

    public void save()
    {
        string yml = m_serializer.Serialize(Configuration);
        lock (m_mutex) {
            WriteConfigFile(yml);
        }
    }

    public void CreateFileWatcher(string path)
    {
        FileSystemWatcher watcher = new FileSystemWatcher();
        watcher.Path = path;
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;
        watcher.Filter = CONFIGURATION_FILE;
        watcher.Changed += new FileSystemEventHandler(OnChanged);
        watcher.Created += new FileSystemEventHandler(OnChanged);
        watcher.Renamed += new RenamedEventHandler(OnRenamed);
        watcher.EnableRaisingEvents = true;
    }

    private void OnRenamed(object source, RenamedEventArgs e) {
        if (e.Name.Equals(CONFIGURATION_FILE)) {
            OnChanged(source, e);
        }
    }

    private void OnChanged(object source, FileSystemEventArgs e)
    {
        Logger logger = LogManager.GetCurrentClassLogger();
        try {
            Thread.Sleep(RETRY_MILLISECONDS);
            if (!File.Exists(m_filename)) {
                return;
            }
            if (e.ChangeType == WatcherChangeTypes.Created || 
                e.ChangeType == WatcherChangeTypes.Renamed || 
                e.ChangeType == WatcherChangeTypes.Changed) {
                SettingConfigFilePermissions(m_filename);
            }
            Load();
            logger = LogManager.GetCurrentClassLogger();
            Changed?.Invoke(this, Configuration);
        } catch (YamlException error) {
            logger.Error("Parsing error: " + error);
        } catch (IOException error) {
            logger.Error(error);
        }
    }
}