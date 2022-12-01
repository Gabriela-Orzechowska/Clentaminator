using System;
using System.IO;

namespace Clentaminator
{
    public static class WiiFormats
    {
        public struct BresHeader
        {
            public string magic;
            public ushort bom;
            public ushort padding;
            public uint length;
            public ushort offsetToRoot;
            public ushort sectionCount;

            public BresHeader(Stream input)
            {
                BigEndianReader reader = new BigEndianReader(input);
                magic = reader.ReadString(4);
                bom = reader.ReadUInt16();
                padding = reader.ReadUInt16();
                length = reader.ReadUInt32();
                offsetToRoot = reader.ReadUInt16();
                sectionCount = reader.ReadUInt16();
            }
        }

        public struct BresRootSection
        {
            public string magic;
            public ulong length;

            public BresRootSection(Stream input)
            {
                BigEndianReader reader = new BigEndianReader(input);
                magic = reader.ReadString(4);
                length = reader.ReadUInt32();
            }
        }
        
        public struct BresIndexGroup
        {
            public long offset;
            public ulong length;
            public ulong number;
            public BresIndexGroupEntry[] entries;

            public BresIndexGroup(Stream input)
            {
                BigEndianReader reader = new BigEndianReader(input);
                offset = input.Position;
                length = reader.ReadUInt32();
                number = reader.ReadUInt32();
                ulong sectionCount = (length - 8) / 16;
                entries = new BresIndexGroupEntry[sectionCount];
                for (int i = 0; i < (int) sectionCount; i++)
                {
                    entries[i] = new BresIndexGroupEntry(input);
                }
            }
            
        }

        public struct BresIndexGroupEntry
        {
            public long offset;
            public ushort entry;
            public ushort unknown;
            public ushort leftIndex;
            public ushort rightIndex;
            public ulong namePointer;
            public ulong dataPointer;

            public BresIndexGroupEntry(Stream input)
            {
                BigEndianReader reader = new BigEndianReader(input);
                offset = input.Position;
                entry = reader.ReadUInt16();
                unknown = reader.ReadUInt16();
                leftIndex = reader.ReadUInt16();
                rightIndex = reader.ReadUInt16();
                namePointer = reader.ReadUInt32();
                dataPointer = reader.ReadUInt32();
            }
        }

        public struct Mdl0Header
        {
            public string magic;
            public ulong length;
            public ulong version;
            public ulong outerBrresOffset;
            public ulong[] sectionsOffsets;
            public ulong nameOffset;

            public Mdl0Header(Stream input)
            {
                BigEndianReader reader = new BigEndianReader(input);
                magic = reader.ReadString(4);
                length = reader.ReadUInt32();
                version = reader.ReadUInt32();
                outerBrresOffset = reader.ReadUInt32();
                sectionsOffsets = new ulong[14];
                for (int i = 0; i < 14; i++)
                {
                    sectionsOffsets[i] = reader.ReadUInt32();
                }
                nameOffset = reader.ReadUInt32();
            }
        }

        public struct Mdl0MaterialSimple
        {
            public ulong dataLenght;
            public ulong textureCount;
            public ulong layerOffset;
            public byte indirectTextureCount;
            public byte[] shaderData;
            public long shaderDataPosition;

            public Mdl0MaterialSimple(Stream input)
            {
                long initialPosition = input.Position;
                BigEndianReader reader = new BigEndianReader(input);
                dataLenght = reader.ReadUInt32();
                input.Position = initialPosition + 0x17;
                indirectTextureCount = reader.ReadByte();
                input.Position = initialPosition + 44;
                textureCount = reader.ReadUInt32();
                layerOffset = reader.ReadUInt32();
                shaderDataPosition = 1048 + (long) (52 * textureCount);
                long shaderDataLenght = (long) dataLenght - shaderDataPosition;
                input.Position = initialPosition + shaderDataPosition;
                shaderData = new byte[shaderDataLenght];
                shaderData = reader.ReadBytes((int)shaderDataLenght);


            }
        }

        public struct Mdl0GraphicCommands
        {
            public Command command;
            public byte address;
            public byte[] data;
            public long offset;

            public Mdl0GraphicCommands(Stream input)
            {
                BigEndianReader reader = new BigEndianReader(input);
                byte value = 0;
                offset = 0;
                command = Command.None;
                do
                {
                    if (input.Position == input.Length)
                    {
                        command = Command.None;
                        value = 0xFF;
                        goto Escape; //I hate myself for that
                    }

                    offset = input.Position;
                    value = reader.ReadByte();

                } while (value == 0);
                Escape:
                command = (Command) value;
                switch (command)
                {
                    case Command.LoadCP:
                        address = reader.ReadByte();
                        data = new byte[4];
                        data = reader.ReadBytes(4);
                        break;
                    case Command.LoadXF:
                        address = 0xFF;
                        ushort transferSize = reader.ReadUInt16();
                        ushort tempAddress = reader.ReadUInt16();
                        int dataSize = 4 * (transferSize + 1);
                        data = new byte[dataSize];
                        data = reader.ReadBytes(dataSize);
                        break;
                    case Command.LoadBP:
                        address = reader.ReadByte();
                        data = new byte[3];
                        data = reader.ReadBytes(3);
                        break;
                    default:
                        address = 0xFF;
                        data = new byte[3];
                        break;
                }

                
            }

            
        }

        public struct Mdl0TevSimple
        {
            public ulong dataLenght;
            public ulong index;
            public byte layerCount;
            public byte[] shaderData;
            public long shaderDataPosition;

            public Mdl0TevSimple(Stream input)
            {
                long initialPosition = input.Position;
                BigEndianReader reader = new BigEndianReader(input);
                dataLenght = reader.ReadUInt32();
                input.Position = initialPosition + 0x08;
                index = reader.ReadUInt32();
                input.Position = initialPosition + 0x0C;
                layerCount = reader.ReadByte();
                shaderDataPosition = 0x20;
                long shaderDataLenght = (long) dataLenght - shaderDataPosition;
                input.Position = initialPosition + shaderDataPosition;
                shaderData = new byte[shaderDataLenght];
                shaderData = reader.ReadBytes((int)shaderDataLenght);
            }
            
        }
        public enum Command
        {
            LoadCP = 0x08,
            LoadXF = 0x10,
            LoadBP = 0x61,
            None = 0xFF
        }
    }
}