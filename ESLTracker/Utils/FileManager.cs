﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ESLTracker.Utils.IOWrappers;
using System.Drawing;
using System.Windows;
using ESLTracker.DataModel;
using System.Reflection;
using ESLTracker.Utils.FileUpdaters;
using System.Xml;

namespace ESLTracker.Utils
{
    public class FileManager
    {

        static string DataPath
        {
            get
            {
                string dp = Properties.Settings.Default.DataPath;
                if(String.IsNullOrWhiteSpace(dp))
                {
                    dp = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    dp = Path.Combine(dp, Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName));
                    Properties.Settings.Default.DataPath = dp;
                    Properties.Settings.Default.Save();
                }
                return dp;
            }
        }

        public static string FullDataFilePath
        {
            get
            {
                return Path.Combine(DataPath, DataFile);
            }
        }

        static string DataFile = "data.xml";
        static string ScreenShotFolder = "Screenshot";


        public static Tracker LoadDatabase()
        {
            Tracker tracker = null;
            try
            {
                tracker = LoadDatabase<DataModel.Tracker>(FullDataFilePath);
                //check for data update
                if (tracker.Version < Tracker.CurrentFileVersion)
                {
                    if (FileManager.UpdateFile(tracker.Version))
                    {
                        //reload after update
                        tracker = LoadDatabase();
                    }
                    else
                    {
                        throw new InvalidDataException("You are using old file format version");
                    }
                }
                //fix up ref to decks in games
                foreach (Game g in tracker.Games)
                {
                    g.Deck = tracker.Decks.Where(d => d.DeckId == g.DeckId).FirstOrDefault();
                }
                //fix up ref to decks in rewards
                foreach (Reward r in tracker.Rewards)
                {
                    r.ArenaDeck = tracker.Decks.Where(d => d.DeckId == r.ArenaDeckId).FirstOrDefault();
                }
                //restore active deck
                Guid? activeDeckFromSettings = Properties.Settings.Default.LastActiveDeckId;
                if ((activeDeckFromSettings != null)
                    && (activeDeckFromSettings != Guid.Empty))
                {
                    tracker.ActiveDeck = tracker.Decks.Where(d => d.DeckId == activeDeckFromSettings).FirstOrDefault();
                }
            }
            catch
            {
                if (tracker != null)
                {
                    if (tracker.Version != Tracker.CurrentFileVersion)
                    {
                        if (FileManager.UpdateFile(tracker.Version))
                        {
                            //reload after update
                            tracker = LoadDatabase();
                        }
                        else
                        {
                            throw;
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
                else
                {
                    if (FileManager.UpdateFile())
                    {
                        //reload after update
                        tracker = LoadDatabase();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return tracker;
        }

        public static T LoadDatabase<T>(string path)
        {
            T tracker;
            //standard serialization
            using (TextReader reader = new StreamReader(path))
            {
                var xml = new XmlSerializer(typeof(T));
                tracker = (T)xml.Deserialize(reader);
            }

            return tracker;
        }

        public static void SaveDatabase()
        {
            SaveDatabase<DataModel.Tracker>(
                FullDataFilePath, 
                DataModel.Tracker.Instance);
        }

        public static void SaveDatabase<T>(string path, T tracker)
        {
            IWrapperProvider wrapperProvider = new WrapperProvider();
            //check if path exist
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            //make backup
            if (File.Exists(path))
            {
                BackupDatabase(wrapperProvider, path);
            }
            //standard serialization
            using (TextWriter writer = new StreamWriter(path))
            {
                var xml = new XmlSerializer(typeof(T));
                xml.Serialize(writer, tracker);
            }
        }

        public static void BackupDatabase(IWrapperProvider wrapperProvider, string path)
        {
            IPathWrapper pathWrapper = wrapperProvider.GetWrapper(typeof(IPathWrapper)) as IPathWrapper;
            IDirectoryWrapper directoryWrapper = wrapperProvider.GetWrapper(typeof(IDirectoryWrapper)) as IDirectoryWrapper;
            IFileWrapper fileWrapper = wrapperProvider.GetWrapper(typeof(IFileWrapper)) as IFileWrapper;
            //copy data.xml to data.xml.bak
            //copy data.xml to data_date.bak - it will override last dayily backup. first run of day will create new one
            string backupFileName = pathWrapper.GetFileNameWithoutExtension(path) + DateTime.Now.ToString("yyyyMMdd");
            string backupPath = pathWrapper.Combine(
                pathWrapper.GetDirectoryName(path),
                backupFileName);
            backupPath = pathWrapper.ChangeExtension(backupPath, pathWrapper.GetExtension(path));

            bool backupExists = File.Exists(backupPath);

            File.Copy(path, backupPath, true);

            if (! backupExists)
            {
                ManageBackups(path, pathWrapper, directoryWrapper, fileWrapper);
            }
        }

        public static void ManageBackups(
            string path, 
            IPathWrapper pathWrapper, 
            IDirectoryWrapper directoryWrapper,
            IFileWrapper fileWrapper)
        {
            string dataFileFilter = Path.ChangeExtension(
                string.Format("{0}*", Path.GetFileNameWithoutExtension(DataFile)),
                Path.GetExtension(DataFile));
            var backupFiles = directoryWrapper.EnumerateFiles(
                            pathWrapper.GetDirectoryName(path),
                            dataFileFilter).OrderBy(f => f);
            //first save of day - delete old backups
            int backupcount = backupFiles.Count();
            int skipfiles = 8; //backups to preserve + data file
            if (backupcount > skipfiles) //7+1 for actual data.xml file
            {
                foreach (string s in backupFiles.Skip(skipfiles))
                {
                    fileWrapper.Delete(s);
                }
            }
        }

        internal static void SaveScreenShot(DependencyObject control)
        {
            IntPtr? eslHandle = WindowsUtils.GetEslProcess()?.MainWindowHandle;
            if (eslHandle.HasValue)
            {
                var rect = new WindowsUtils.Rect();
                WindowsUtils.GetWindowRect(eslHandle.Value, ref rect);

                int width = rect.right - rect.left;
                int height = rect.bottom - rect.top;

                var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                Graphics gfxBmp = Graphics.FromImage(bmp);

                Window.GetWindow(control).Hide();
                gfxBmp.CopyFromScreen(
                    rect.left,
                    rect.top,
                    0,
                    0,
                    new System.Drawing.Size(width, height),
                    CopyPixelOperation.SourceCopy);
                Window.GetWindow(control).Show();

                string path = Path.Combine(
                    DataPath,
                    ScreenShotFolder,
                    Path.ChangeExtension(ScreenShotFolder + DateTime.Now.ToString("yyyyMMddHHmmss"),
                    "png"));
                if (!Directory.Exists(Path.GetDirectoryName(path)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                }
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);

                gfxBmp.Dispose();

            }
        }

        internal static bool UpdateFile()
        {
            return UpdateFile(ReadCurrentFileVersionFromXML());
        }

        internal static bool UpdateFile(SerializableVersion fromVersion)
        {
            if (fromVersion == null)
            {
                return false;
            }
            IEnumerable<Type> updaterTypes = FindUpdateClass(fromVersion);
            if (updaterTypes.Count() != 1)
            {
                //no updater found, or too many
                return false;
            }
            IFileUpdater updater = (IFileUpdater)Activator.CreateInstance(updaterTypes.First());
            return updater.UpdateFile();
        }


        private static SerializableVersion ReadCurrentFileVersionFromXML()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(FileManager.FullDataFilePath);
            XmlNode versionNode = doc.SelectSingleNode("/Tracker/Version");

            return ParseCurrentFileVersion(versionNode);
        }

        public static SerializableVersion ParseCurrentFileVersion(XmlNode versionNode)
        {
            if (versionNode == null)
            {
                return null;
            }

            bool allOK = true;
            SerializableVersion ret = new SerializableVersion();
            allOK &= Int32.TryParse(versionNode.SelectSingleNode("Build")?.InnerText, out ret.Build);
            allOK &= Int32.TryParse(versionNode.SelectSingleNode("Major")?.InnerText, out ret.Major);
            allOK &= Int32.TryParse(versionNode.SelectSingleNode("Minor")?.InnerText, out ret.Minor);
            allOK &= Int32.TryParse(versionNode.SelectSingleNode("Revision")?.InnerText, out ret.Revision);

            if (!allOK)
            {
                //parse failed
                return null;
            }
            return ret;
        }


        private static IEnumerable<Type> FindUpdateClass(SerializableVersion currentFileVersion)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    SerializableVersion upgradeFrom = type.GetCustomAttributes(typeof(SerializableVersion), true).FirstOrDefault() as SerializableVersion;
                    if (upgradeFrom == currentFileVersion)
                    {
                        yield return type;
                    }
                }
            }
        }

        internal static string CreateNewVersionXML(SerializableVersion TargetVersion)
        {
            StringBuilder serialisedVersion = new StringBuilder();
            using (TextWriter writer = new StringWriter(serialisedVersion))
            {
                var xml = new XmlSerializer(typeof(SerializableVersion), String.Empty);
                xml.Serialize(writer, TargetVersion);
            }

            XmlDocument newVersionDoc = new XmlDocument();
            newVersionDoc.LoadXml(serialisedVersion.ToString());

            return newVersionDoc.DocumentElement.InnerXml;
        }
    }
}
