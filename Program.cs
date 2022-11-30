using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clentaminator
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (var arg in args)
            {
                if(File.Exists(arg))
                {
                    int fixedMaterials = Purifier.PurifyMdl0(arg);
                    if(fixedMaterials != -1)
                        Console.WriteLine("File: {0}, Fixed Materials: {1}",arg,fixedMaterials);
                }
            }
            
        }
    }

    public static class Purifier
    {
        public static int fixedMaterials = 0;
        public static int PurifyMdl0(string path)
        {
            Stream input = File.Open(path, FileMode.Open);
            
            long mdl0Offset = GetOffset(input,path);
            if (mdl0Offset == -1) return -1;
            input.Position = mdl0Offset;
            
            var mdl0Header = new WiiFormats.Mdl0Header(input);
            
            
            input.Position = mdl0Offset + (long) mdl0Header.sectionsOffsets[8];
            WiiFormats.BresIndexGroup materialGroup = new WiiFormats.BresIndexGroup(input);
            for (int i = 1; i < materialGroup.entries.Length; i++)
            {
                var materialDataOffset = materialGroup.offset + (long) materialGroup.entries[i].dataPointer;
                input.Position = materialDataOffset;
                PurifyMaterial(input,materialDataOffset);
            }

            return fixedMaterials;



        }

        private static void PurifyMaterial(Stream input,long initialOffset)
        {
            WiiFormats.Mdl0MaterialSimple material = new WiiFormats.Mdl0MaterialSimple(input);
            List<WiiFormats.Mdl0MaterialShader> shaderCommands = new List<WiiFormats.Mdl0MaterialShader>();
            WiiFormats.Mdl0MaterialShader currentShaderCommand;
            byte[] shaderData = material.shaderData;
            long shaderDataStart = input.Position - (long) material.dataLenght + material.shaderDataPosition;
            Stream byteStream = new MemoryStream(shaderData);
            var matrixFound = false;
            var isIndirect = false;
            long offset26 = 0;
            do
            {
                currentShaderCommand = new WiiFormats.Mdl0MaterialShader(byteStream);
                shaderCommands.Add(currentShaderCommand);
                byte[] data = currentShaderCommand.data;
                if (material.indirectTextureCount > 0)
                {
                    isIndirect = true;
                    if (currentShaderCommand.address == 0x06) matrixFound = true;
                    else if (currentShaderCommand.address == 0x26) offset26 = currentShaderCommand.offset + initialOffset + material.shaderDataPosition;
                    
                }
                
            } while (currentShaderCommand.command != WiiFormats.Mdl0MaterialShader.Command.None);
            if (isIndirect && !matrixFound)
            {
                FixMatrix(input, offset26);
            }

        }
        
        public static void FixMatrix(Stream input,long offset)
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
                input.Position = offset + 5;
                input.Write(dataToFix,0,dataToFix.Length);    
                input.Position = offset + 5;
                input.Write(dataToFix,0,dataToFix.Length);
                fixedMaterials += 1;
            }

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

