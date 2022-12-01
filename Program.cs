using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Clentaminator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: clentaminator <file_paths>");
            }
            foreach (var arg in args)
            {
                if(File.Exists(arg))
                {
                    int[] purify = Purifier.PurifyMdl0(arg);
                    Console.WriteLine("File: {0}",arg);
                    if(purify[2] == 0) Console.WriteLine("      No corruptions found.");
                    else
                    {
                        Console.WriteLine("      Materials fixed: {0}",purify[2]);
                        Console.WriteLine("      Missing Register Calls: {0}",purify[0]*3);
                        Console.WriteLine("      Invalid indirect stage count: {0}",purify[1]);
                    }
                    
                }
                else
                {
                    Console.WriteLine("Could not find the file: {0}",arg);
                }
            }
            
        }
    }

    public static class Purifier
    {
        public static int FixedMissingRegisterCalls = 0;
        public static int FixedDisabledIndirectStages = 0;
        public static int FixedMaterials = 0;
        public static int[] PurifyMdl0(string path)
        {
            Stream input = File.Open(path, FileMode.Open);
            
            long mdl0Offset = GetOffset(input,path);
            if (mdl0Offset == -1) return new int[2] {-1,-1};
            input.Position = mdl0Offset;
            
            var mdl0Header = new WiiFormats.Mdl0Header(input);
            
            
            input.Position = mdl0Offset + (long) mdl0Header.sectionsOffsets[8];
            WiiFormats.BresIndexGroup materialGroup = new WiiFormats.BresIndexGroup(input);
            input.Position = mdl0Offset + (long) mdl0Header.sectionsOffsets[9];
            WiiFormats.BresIndexGroup tevGroup = new WiiFormats.BresIndexGroup(input);
            List<int> indirectStagesList = new();
            for (int i = 1; i < tevGroup.entries.Length; i++)
            {
                var tevDataOffset = tevGroup.offset + (long) tevGroup.entries[i].dataPointer;
                input.Position = tevDataOffset;
                indirectStagesList.Add(GetIndirectStages(input));
            }

            int[] indirectStages = indirectStagesList.ToArray();
            for (int i = 1; i < materialGroup.entries.Length; i++)
            {
                var materialDataOffset = materialGroup.offset + (long) materialGroup.entries[i].dataPointer;
                input.Position = materialDataOffset;
                PurifyMaterials(input,materialDataOffset,indirectStages[i-1]);
            }
            return new int[3] {FixedMissingRegisterCalls,FixedDisabledIndirectStages,FixedMaterials};
        }

        private static int GetIndirectStages(Stream input)
        {
            WiiFormats.Mdl0TevSimple tev = new WiiFormats.Mdl0TevSimple(input);
            List<WiiFormats.Mdl0GraphicCommands> shaderCommands = new();
            WiiFormats.Mdl0GraphicCommands currentShaderCommand;
            byte[] shaderData = tev.shaderData;
            Stream byteStream = new MemoryStream(shaderData);
            int indirectCalls = 0;
            
            do
            {
                currentShaderCommand = new WiiFormats.Mdl0GraphicCommands(byteStream);
                shaderCommands.Add(currentShaderCommand);
                if (0x10 <= currentShaderCommand.address && currentShaderCommand.address <= 0x1F)
                {
                    int dataSum = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        dataSum += currentShaderCommand.data[i];
                    }
                    if(dataSum != 0)
                    {
                        indirectCalls++;

                    }
                }
            } while (currentShaderCommand.command != WiiFormats.Command.None);
            return indirectCalls;
        }
        

        private static void PurifyMaterials(Stream input,long initialOffset, int indirectCalls)
        {
            input.Position = initialOffset + 0x17;
            var curData = input.ReadByte();
            input.Position = initialOffset + 0x17;
            int enableIndirectStage = indirectCalls > 0 ? 1 : 0; //I have no idea how to actually count indirect shader stages; haven't seen any with more than 1 so idc
            input.WriteByte((byte) enableIndirectStage);
            input.Position = initialOffset;
            WiiFormats.Mdl0MaterialSimple material = new WiiFormats.Mdl0MaterialSimple(input);
            List<WiiFormats.Mdl0GraphicCommands> shaderCommands = new();
            WiiFormats.Mdl0GraphicCommands currentShaderCommand;
            byte[] shaderData = material.shaderData;
            Stream byteStream = new MemoryStream(shaderData);
            var isIndirect = false;
            long offset26 = 0;
            do
            {
                currentShaderCommand = new WiiFormats.Mdl0GraphicCommands(byteStream);
                shaderCommands.Add(currentShaderCommand);
                if (material.indirectTextureCount > 0)
                {
                    isIndirect = true;
                    if (currentShaderCommand.address == 0x26) offset26 = currentShaderCommand.offset + initialOffset + material.shaderDataPosition;
                    
                }
                
            } while (currentShaderCommand.command != WiiFormats.Command.None);

            bool fixedMatrix = false;
            if (isIndirect)
            {
                fixedMatrix = FixMatrix(input, offset26);
            }

            if (curData != enableIndirectStage)
            {
                FixedDisabledIndirectStages++;
                FixedMaterials++;
            }
            else if (fixedMatrix)
            {
                FixedMaterials++;
            }

        }
        
        public static bool FixMatrix(Stream input,long offset)
        {
            byte[] dataToFix = {0x61,0x06,0x00,0x03,0xD7,0x61,0x07,0xDE,0xB8,0x00,0x61,0x08,0x00,0x00,0x00};
            input.Position = offset + 5;
            BigEndianReader reader = new BigEndianReader(input);
            var check = 0;
            for (int i = 0; i < 15; i++)
            {
                check += reader.ReadByte();
            }
            if(check == 0)
            {
                //Please don't ask, at least it works
                input.Position = offset + 5;
                input.Write(dataToFix,0,dataToFix.Length);
                input.Position = offset + 5;
                input.Write(dataToFix,0,dataToFix.Length);
                FixedMissingRegisterCalls += 1;
                return true;
            }

            return false;
        }
        private static int GetOffset(Stream input, string filename)
        {
            string format = Mdl0Decoder.GetFormat(input);
            switch (format)
            {
                case "Yaz0":
                    Console.WriteLine("File {0}: Could not read the file: YAZ0 archives are not supported.",filename);
                    return -1;
                case "bres":
                    return (int) Mdl0Decoder.Mdl0Offset(input);
                case "MDL0":
                    return 0;
                default:
                    Console.WriteLine("File {0}: Could not read the file: Unknown format",filename);
                    return -1;
            }
            
        }
    }

    public static class Mdl0Decoder
    {
        public static string GetFormat(Stream input)
        {
            
            WiiFormats.BresHeader header = new WiiFormats.BresHeader(input); //Read File Header
            return header.magic;

        }

        public static long Mdl0Offset(Stream input)
        {
            var root = new WiiFormats.BresRootSection(input); //Reader BRRES root
            var group = new WiiFormats.BresIndexGroup(input); //Read BRRES main Index Group
            var sectionCount = group.entries.Length; //Read number of BRRES Sections
            long[] sectionOffsets = new long[sectionCount]; 
            for (var i = 0; i < sectionCount; i++)
            {
                sectionOffsets[i] = group.offset + (long) group.entries[i].dataPointer; //Get sections' offsets
            }
            //Get Group of every section, return the offset to MDL0
            WiiFormats.BresIndexGroup[] sections = new WiiFormats.BresIndexGroup[sectionCount]; 
            for (int i = 0; i < sectionCount; i++)
            {
                input.Position = sectionOffsets[i];
                sections[i] = new WiiFormats.BresIndexGroup(input);
                long thisOffset = sections[i].offset + (long) sections[i].entries[i].dataPointer;
                input.Position = thisOffset;
                BigEndianReader reader = new BigEndianReader(input);
                string magic = reader.ReadString(4);
                if (magic == "MDL0") return thisOffset;
            }

            return 0;
        }
    }

    
}

