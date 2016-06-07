﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CASCExplorer;
using System.Reflection;

namespace PackageTool {
  class Program {
    private static void CopyBytes(Stream i, Stream o, int sz) {
      byte[] buffer = new byte[sz];
      i.Read(buffer, 0, sz);
      o.Write(buffer, 0, sz);
      buffer = null;
    }

    static void Main(string[] args) {
      if(args.Length < 3) {
        Console.Out.WriteLine("Usage: PackageTool.exe \"overwatch_folder\" \"output_folder\" <keys...>");
        Console.Out.WriteLine("Keys must start with 'i' for content keys or 'p' for package keys");
        return;
      }

      string root = args[0];
      string output = args[1] + Path.DirectorySeparatorChar;

      Console.Out.WriteLine("{0} v{1}", Assembly.GetExecutingAssembly().GetName().Name, Assembly.GetExecutingAssembly().GetName().Version.ToString());

      List<ulong> packageKeys = new List<ulong>();
      List<string> contentKeys = new List<string>();

      for(int i = 2; i < args.Length; ++i) {
        string arg = args[i];
        switch(arg[0]) {
          case 'p':
            packageKeys.Add(ulong.Parse(arg.Substring(1), NumberStyles.HexNumber));
            break;
          case 'P':
            packageKeys.Add(ulong.Parse(arg.Substring(1), NumberStyles.Number));
            break;
          case 'i':
            contentKeys.Add(arg.Substring(1).ToUpperInvariant());
            break;
        }
      }

      if(contentKeys.Count + packageKeys.Count == 0) {
        Console.Error.WriteLine("Must have at least 1 query");
        return;
      }
      
      CASCConfig config = CASCConfig.LoadLocalStorageConfig(root);
      CASCHandler handler = CASCHandler.OpenStorage(config);
      OwRootHandler ow = handler.Root as OwRootHandler;
      if(ow == null) {
        Console.Error.WriteLine("Not a valid Overwatch installation");
        return;
      }

      foreach(APMFile apm in ow.APMFiles) {
        for(long i = 0; i < apm.Packages.LongLength; ++i) {
          if(packageKeys.Count + contentKeys.Count == 0) {
            return;
          }

          APMPackage package = apm.Packages[i];

          bool ret = true;
          if(packageKeys.Count > 0 && packageKeys.Contains(package.packageKey)) {
            ret = false;
          }

          if(ret && contentKeys.Count > 0 && contentKeys.Contains(package.indexContentKey.ToHexString().ToUpperInvariant())) {
            ret = false;
          }

          if(ret) {
            continue;
          }

          packageKeys.Remove(package.packageKey);
          contentKeys.Remove(package.indexContentKey.ToHexString().ToUpperInvariant());

          PackageIndex index = apm.Indexes[i];
          PackageIndexRecord[] records = apm.Records[i];

          string o = string.Format("{0}{1}{2}", output, package.indexContentKey.ToHexString(), Path.DirectorySeparatorChar);
          EncodingEntry bundleEncoding;
          if(!handler.Encoding.GetEntry(index.bundleContentKey, out bundleEncoding)) {
            Console.Error.WriteLine("Cannot open bundle");
            continue;
          }
          if(!Directory.Exists(o)) {
            Directory.CreateDirectory(o);
          }

          using(Stream bundleStream = handler.OpenFile(bundleEncoding.Key)) {
            foreach(PackageIndexRecord record in records) {
              ulong rtype = OWLib.APM.keyToTypeID(record.Key);
              ulong rindex = OWLib.APM.keyToIndexID(record.Key);
              string ofn = string.Format("{0}{1:X3}{2}", o, rtype, Path.DirectorySeparatorChar);
              if(!Directory.Exists(ofn)) {
                Directory.CreateDirectory(ofn);
              }
              ofn = string.Format("{0}{1:X12}.{2:X3}", ofn, rindex, rtype);

              using(Stream outputStream = File.Open(ofn, FileMode.Create, FileAccess.Write)) {
                if(((ContentFlags)record.Flags & ContentFlags.Bundle) == ContentFlags.Bundle) {
                  bundleStream.Position = record.Offset;
                  CopyBytes(bundleStream, outputStream, record.Size);
                } else {
                  EncodingEntry recordEncoding;
                  if(!handler.Encoding.GetEntry(record.ContentKey, out recordEncoding)) {
                    Console.Error.WriteLine("Cannot open file {0} -- doesn't have bundle flags", ofn);
                    continue;
                  }

                  using(Stream recordStream = handler.OpenFile(recordEncoding.Key)) {
                    CopyBytes(recordStream, outputStream, record.Size);
                  }
                }
              }

              Console.Out.WriteLine("Saved file {0}", ofn);
            }
          }
        }
      }
    }
  }
}