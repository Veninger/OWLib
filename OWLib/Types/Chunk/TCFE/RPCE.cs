﻿using System.IO;
using System.Runtime.InteropServices;

namespace OWLib.Types.Chunk {
    public class RPCE : IChunk {
        public string Identifier => "RPCE"; // ECPR - Effect Particle?
        public string RootIdentifier => "TCFE"; // EFCT - Effect

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct Structure {
            public ulong unknown1;
            public ulong unknown2;
            public ulong unknown3;
            public ulong unknown4;
            public ulong model_key;
        }

        private Structure data;
        public Structure Data => data;

        public void Parse(Stream input) {
            using (BinaryReader reader = new BinaryReader(input, System.Text.Encoding.Default, true)) {
                data = reader.Read<Structure>();
            }
        }
    }
}
