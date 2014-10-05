﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace TheNoobViewer
{
    internal class IndexEntry
    {
        public int Index;
        public int Offset;
        public int Size;
    }

    public class CASCHandler
    {
        private readonly LocalIndexHandler LocalIndex;
        private readonly CDNIndexHandler CDNIndex;

        private readonly EncodingHandler EncodingHandler;
        private readonly WowRootHandler RootHandler;

        private static readonly Jenkins96 Hasher = new Jenkins96();

        private readonly Dictionary<int, FileStream> DataStreams = new Dictionary<int, FileStream>();

        public int NumRootEntries { get { return RootHandler.Count; } }
        public int NumRootEntriesTotal { get { return RootHandler.RootData.Sum(re => re.Value.Count); } }
        public int NumRootEntriesSelect { get; private set; }
        public int NumUnknownFiles { get; private set; }

        private readonly CASCConfig config;

        public Dictionary<ulong, bool> UnknownFiles = new Dictionary<ulong, bool>();

        private CASCHandler(CASCConfig config, AsyncAction worker)
        {
            this.config = config;

            CDNIndex = CDNIndexHandler.Initialize(config, worker);

            if (!config.OnlineMode)
                LocalIndex = LocalIndexHandler.Initialize(config, worker);

            using (var fs = OpenEncodingFile())
                EncodingHandler = new EncodingHandler(fs, worker);

            Logger.WriteLine("CASCHandler: loaded {0} encoding data", EncodingHandler.Count);

            using (var fs = OpenRootFile())
                RootHandler = new WowRootHandler(fs, worker);

            Logger.WriteLine("CASCHandler: loaded {0} root data", RootHandler.Count);
        }

        private Stream OpenRootFile()
        {
            var encInfo = EncodingHandler.GetEncodingInfo(config.RootMD5);

            if (encInfo == null)
                throw new FileNotFoundException("encoding info for root file missing!");

            if (encInfo.Keys.Count > 1)
                throw new FileNotFoundException("multiple encoding info for root file found!");

            Stream s = TryLocalCache(encInfo.Keys[0], config.RootMD5, Path.Combine("data", config.Build.ToString(), "root"));

            if (s != null)
                return s;

            s = TryLocalCache(encInfo.Keys[0], config.RootMD5, Path.Combine("data", config.Build.ToString(), "root"));

            if (s != null)
                return s;

            return OpenFile(encInfo.Keys[0]);
        }

        private Stream OpenEncodingFile()
        {
            Stream s = TryLocalCache(config.EncodingKey, config.EncodingMD5, Path.Combine("data", config.Build.ToString(), "encoding"));

            if (s != null)
                return s;

            s = TryLocalCache(config.EncodingKey, config.EncodingMD5, Path.Combine("data", config.Build.ToString(), "encoding"));

            if (s != null)
                return s;

            return OpenFile(config.EncodingKey);
        }

        private Stream TryLocalCache(byte[] key, byte[] md5, string name)
        {
            if (File.Exists(name))
            {
                var fs = File.OpenRead(name);

                if (MD5.Create().ComputeHash(fs).EqualsTo(md5))
                {
                    fs.Position = 0;
                    return fs;
                }

                fs.Close();

                File.Delete(name);
            }

            ExtractFile(key, ".", name);

            return null;
        }

        private Stream OpenFile(byte[] key)
        {
            try
            {
                if (config.OnlineMode)
                    throw new Exception("OnlineMode=true");

                var idxInfo = LocalIndex.GetIndexInfo(key);

                if (idxInfo == null)
                    throw new Exception("local index missing");

                var stream = GetDataStream(idxInfo.Index);

                stream.Position = idxInfo.Offset;

                stream.Position += 30;

                using (BLTEHandler blte = new BLTEHandler(stream, idxInfo.Size - 30))
                {
                    return blte.OpenFile();
                }
            }
            catch
            {
                var idxInfo = CDNIndex.GetIndexInfo(key);

                if (idxInfo != null)
                {
                    using (Stream s = CDNIndex.OpenDataFile(key))
                    using (BLTEHandler blte = new BLTEHandler(s, idxInfo.Size))
                    {
                        return blte.OpenFile();
                    }
                }
                else
                {
                    try
                    {
                        using (Stream s = CDNIndex.OpenDataFileDirect(key))
                        using (BLTEHandler blte = new BLTEHandler(s, (int)s.Length))
                        {
                            return blte.OpenFile();
                        }
                    }
                    catch
                    {
                        throw new Exception("CDN index missing");
                    }
                }
            }
        }

        private void ExtractFile(byte[] key, string path, string name)
        {
            try
            {
                if (config.OnlineMode)
                    throw new Exception("OnlineMode=true");

                var idxInfo = LocalIndex.GetIndexInfo(key);

                if (idxInfo == null)
                    throw new Exception("local index missing");

                var stream = GetDataStream(idxInfo.Index);

                stream.Position = idxInfo.Offset;

                stream.Position += 30;
                //byte[] unkHash = reader.ReadBytes(16);
                //int __size = reader.ReadInt32();
                //byte[] unkData1 = reader.ReadBytes(2);
                //byte[] unkData2 = reader.ReadBytes(8);

                using (BLTEHandler blte = new BLTEHandler(stream, idxInfo.Size - 30))
                {
                    blte.ExtractToFile(path, name);
                }
            }
            catch
            {
                var idxInfo = CDNIndex.GetIndexInfo(key);

                if (idxInfo != null)
                {
                    using (Stream s = CDNIndex.OpenDataFile(key))
                    using (BLTEHandler blte = new BLTEHandler(s, idxInfo.Size))
                    {
                        blte.ExtractToFile(path, name);
                    }
                }
                else
                {
                    try
                    {
                        using (Stream s = CDNIndex.OpenDataFileDirect(key))
                        using (BLTEHandler blte = new BLTEHandler(s, (int)s.Length))
                        {
                            blte.ExtractToFile(path, name);
                        }
                    }
                    catch
                    {
                        throw new Exception("CDN index missing");
                    }
                }
            }
        }

        ~CASCHandler()
        {
            Console.WriteLine("All closed !!!!");
            foreach (var stream in DataStreams)
                stream.Value.Close();
        }

        public List<RootEntry> GetRootInfo(ulong hash)
        {
            return RootHandler.GetRootInfo(hash);
        }

        public EncodingEntry GetEncodingInfo(byte[] md5)
        {
            return EncodingHandler.GetEncodingInfo(md5);
        }

        private FileStream GetDataStream(int index)
        {
            FileStream stream;
            if (DataStreams.TryGetValue(index, out stream))
                return stream;

            string dataFile = Path.Combine(config.BasePath, String.Format("Data\\data\\data.{0:D3}", index));

            stream = new FileStream(dataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            DataStreams[index] = stream;

            return stream;
        }

        public static CASCHandler OpenLocalStorage(string basePath, AsyncAction worker = null)
        {
            CASCConfig config = CASCConfig.LoadLocalStorageConfig(basePath);

            return Open(worker, config);
        }

        public static CASCHandler OpenOnlineStorage(string product, AsyncAction worker = null)
        {
            CASCConfig config = CASCConfig.LoadOnlineStorageConfig(product);

            return Open(worker, config);
        }

        private static CASCHandler Open(AsyncAction worker, CASCConfig config)
        {
            return new CASCHandler(config, worker);
        }

        public void LoadListFile(Stream path, AsyncAction worker = null)
        {
            if (worker != null)
            {
                worker.ThrowOnCancel();
                //worker.ReportProgress(0);
            }

            using (var sr = new StreamReader(path))
            {
                string file;

                while ((file = sr.ReadLine()) != null)
                {
                    ulong fileHash = Hasher.ComputeHash(file);

                    // skip invalid names
                    if (!RootHandler.RootData.ContainsKey(fileHash))
                    {
                        Logger.WriteLine("Invalid file name: {0}", file);
                        continue;
                    }

                    CASCFile.FileNames[fileHash] = file;

                    if (worker != null)
                    {
                        worker.ThrowOnCancel();
                        //worker.ReportProgress((int)((float)sr.BaseStream.Position / (float)sr.BaseStream.Length * 100));
                    }
                }

                Logger.WriteLine("CASCHandler: loaded {0} valid file names", CASCFile.FileNames.Count);
            }
        }

        public CASCFolder CreateStorageTree(LocaleFlags locale)
        {
            var rootHash = Hasher.ComputeHash("root");

            var root = new CASCFolder(rootHash);

            CASCFolder.FolderNames[rootHash] = "root";

            NumRootEntriesSelect = 0;

            // Cleanup fake names for unknown files
            NumUnknownFiles = 0;

            foreach (var unkFile in UnknownFiles)
                CASCFile.FileNames.Remove(unkFile.Key);

            //Stream sw = new FileStream("unknownHashes.dat", FileMode.Create);
            //BinaryWriter bw = new BinaryWriter(sw);

            // Create new tree based on specified locale
            foreach (var rootEntry in RootHandler.RootData)
            {
                if (!rootEntry.Value.Any(re => (re.Block.LocaleFlags & locale) != 0))
                    continue;

                string file;

                if (!CASCFile.FileNames.TryGetValue(rootEntry.Key, out file))
                {
                    //file = "unknown\\" + rootEntry.Key.ToString("X16");
                    //NumUnknownFiles++;
                    //UnknownFiles[rootEntry.Key] = true;
                }
                else
                {
                    CreateSubTree(root, rootEntry.Key, file);
                    NumRootEntriesSelect++;
                }
            }

            //bw.Flush();
            //bw.Close();

            Logger.WriteLine("CASCHandler: {0} file names missing", NumUnknownFiles);

            return root;
        }

        private static void CreateSubTree(CASCFolder root, ulong filehash, string file)
        {
            string[] parts = file.Split('\\');

            CASCFolder folder = root;

            for (int i = 0; i < parts.Length; ++i)
            {
                bool isFile = (i == parts.Length - 1);

                ulong hash = isFile ? filehash : Hasher.ComputeHash(parts[i]);

                ICASCEntry entry = folder.GetEntry(hash);

                if (entry == null)
                {
                    if (isFile)
                    {
                        entry = new CASCFile(hash);
                        CASCFile.FileNames[hash] = file;
                    }
                    else
                    {
                        entry = new CASCFolder(hash);
                        CASCFolder.FolderNames[hash] = parts[i];
                    }

                    folder.SubEntries[hash] = entry;
                }

                folder = entry as CASCFolder;
            }
        }

        public bool FileExist(string file)
        {
            var hash = Hasher.ComputeHash(file);
            var rootInfos = RootHandler.GetRootInfo(hash);
            return rootInfos != null && rootInfos.Count > 0;
        }

        private EncodingEntry GetEncodingEntry(ulong hash, LocaleFlags locale, ContentFlags content)
        {
            var rootInfos = RootHandler.GetRootInfo(hash);

            var rootInfosLocale = rootInfos.Where(re => (re.Block.LocaleFlags & locale) != 0);

            if (rootInfosLocale.Count() > 1)
            {
                if (content != ContentFlags.None)
                {
                    var rootInfosLocaleAndContent = rootInfosLocale.Where(re => (re.Block.ContentFlags & content) == 0);

                    if (rootInfosLocaleAndContent.Any())
                        rootInfosLocale = rootInfosLocaleAndContent;
                }
            }

            return EncodingHandler.GetEncodingInfo(rootInfosLocale.First().MD5);
        }

        private EncodingEntry GetEncodingEntryOld(ulong hash, LocaleFlags locale, ContentFlags content)
        {
            var rootInfos = RootHandler.GetRootInfo(hash);

            foreach (var rootInfo in rootInfos)
            {
                if ((rootInfo.Block.LocaleFlags & locale) != 0 && (rootInfo.Block.ContentFlags & content) == 0)
                {
                    var encInfo = EncodingHandler.GetEncodingInfo(rootInfo.MD5);

                    if (encInfo != null)
                        return encInfo;
                }
            }

            return null;
        }

        public Stream OpenFile(string fullName, LocaleFlags locale, ContentFlags content = ContentFlags.None)
        {
            var hash = Hasher.ComputeHash(fullName);

            return OpenFile(hash, fullName, locale, content);
        }

        public Stream OpenFile(ulong hash, string fullName, LocaleFlags locale, ContentFlags content = ContentFlags.None)
        {
            EncodingEntry encInfo = GetEncodingEntry(hash, locale, content);

            if (encInfo != null)
                return OpenFile(encInfo.Keys[0]);

            throw new FileNotFoundException(fullName);
        }

        public void SaveFileTo(string fullName, string extractPath, LocaleFlags locale, ContentFlags content = ContentFlags.None)
        {
            var hash = Hasher.ComputeHash(fullName);

            SaveFileTo(hash, fullName, extractPath, locale, content);
        }

        public void SaveFileTo(ulong hash, string fullName, string extractPath, LocaleFlags locale, ContentFlags content = ContentFlags.None)
        {
            EncodingEntry encInfo = GetEncodingEntry(hash, locale, content);

            if (encInfo != null)
            {
                ExtractFile(encInfo.Keys[0], extractPath, fullName);
                return;
            }

            throw new FileNotFoundException(fullName);
        }
    }
}
