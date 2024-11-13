using System;
using System.Collections.Generic;
using System.Text;

namespace HASM
{
    class Program
    {
        static void Main(string[] args)
        {
            string code = @"
$define SYS_EXIT 60
$define SYS_WRITE 1
$define STDOUT 1

$global _start;

$section text {
    label _start {
        move rax <- SYS_WRITE;
        move rdi <- STDOUT;
        move rsi <- message;
        move rdx <- message_length;
        move rax <- &SYS_EXIT;
        move rdi <- 0;
    }
}

$section data {
    label message {
        db 'Hello, World!', 0;
    }
    label message_length {
        dq $ - message;
    }
}
";

            HAssembler assembler = new HAssembler(code);
            Console.WriteLine(assembler.Assemble());
        }
    }

    public class Tokenizer
    {
        private readonly string[] tokens;
        private int position;

        public Tokenizer(string code)
        {
            // Splitting code by spaces and newlines while preserving braces as separate tokens
            tokens = code.Replace("{", " { ").Replace("}", " } ").Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            position = 0;
        }

        public string GetNext()
        {
            return position < tokens.Length ? tokens[position++] : null;
        }

        public string Peek()
        {
            return position < tokens.Length ? tokens[position] : null;
        }

        public bool HasMoreTokens()
        {
            return position < tokens.Length;
        }

        public string GetBlock()
        {
            int braceCount = 0;
            List<string> blockTokens = new List<string>();

            // Expecting an opening brace
            if (GetNext() != "{")
            {
                throw new Exception("Expected '{' to start a block");
            }

            // Collecting tokens until matching closing brace
            while (HasMoreTokens())
            {
                string token = GetNext();
                if (token == "{") braceCount++;
                if (token == "}") braceCount--;
                
                if (braceCount < 0)
                    break;

                blockTokens.Add(token);
            }

            return string.Join(" ", blockTokens);
        }
    }

    public class HAssembler
    {
        private readonly Dictionary<string, string> PreProcessingDefines = new Dictionary<string, string>();
        private readonly Tokenizer tokenizer;
        private readonly int indentationLevel;

        public HAssembler(string code, int indentationLevel = 0)
        {
            tokenizer = new Tokenizer(code);
            this.indentationLevel = indentationLevel;
        }

        /// <summary>
        /// Assemble the code and return the NASM code.
        /// </summary>
        /// <returns>The NASM code</returns>
        public string Assemble()
        {
            StringBuilder output = new StringBuilder();

            while (tokenizer.HasMoreTokens())
            {
                string token = tokenizer.GetNext();

                if (token == "$define")
                {
                    string name = tokenizer.GetNext();
                    string value = tokenizer.GetNext();
                    PreProcessingDefines[name] = value;
                }
                else if (token == "$undef")
                {
                    string name = tokenizer.GetNext();
                    PreProcessingDefines.Remove(name);
                }
                else if (token == "$section")
                {
                    string sectionType = tokenizer.GetNext().ToLower();
                    output.AppendLine($"section .{sectionType}");

                    // Parse the block associated with the section
                    string blockContent = tokenizer.GetBlock();
                    output.Append(new HAssembler(blockContent, indentationLevel + 1).Assemble());
                }
                else if (token == "$global")
                {
                    string globalName = tokenizer.GetNext();
                    output.AppendLine($"global {globalName}");
                }
                else if (token == "label")
                {
                    string labelName = tokenizer.GetNext();
                    output.AppendLine($"{labelName}:");

                    // Parse the block associated with the label
                    string labelBlock = tokenizer.GetBlock();
                    output.Append(new HAssembler(labelBlock, indentationLevel + 1).Assemble());
                }
                else
                {
                    //check if a token contains a define
                    if (PreProcessingDefines.ContainsKey(token))
                    {
                        token = PreProcessingDefines[token];
                    }
                    if (token == "move")
                    {
                        // example: move rax <- 0x10, OR, rax <- rbx, or rax <- &0x1234 (memory)
                        string destination = tokenizer.GetNext();
                        string arrow = tokenizer.GetNext();
                        string source = tokenizer.GetNext();
                        //go through the source and destination and check if they are a define
                        if (PreProcessingDefines.ContainsKey(destination))
                        {
                            destination = PreProcessingDefines[destination];
                        }
                        if (PreProcessingDefines.ContainsKey(source))
                        {
                            source = PreProcessingDefines[source];
                        }
                        // Check if the source is a memory address, register
                        if (source.StartsWith("&"))
                        {
                            output.AppendLine($"mov {destination}, [{source.Substring(1)}]");
                        }
                        else if (source.StartsWith("0x") || source.StartsWith("0b") || source.StartsWith("0o") || long.TryParse(source, out long _))
                        {
                            output.AppendLine($"mov {destination}, {NumberHelper.GetNumber(source)}");
                        }
                        else
                        {
                            output.AppendLine($"mov {destination}, {source}");
                        }
                    }
                    // else check if theres an arrow, if so, just put a comma
                    else
                    {
                        //check if a <-
                        if (tokenizer.Peek() == "<-")
                        {
                            output.Append($"{token}, ");
                        }
                        else if (token.StartsWith("&"))
                        {
                            output.Append($"[{token.Substring(1)}] ");
                        }
                        else if (token.StartsWith("0x") || token.StartsWith("0b") || token.StartsWith("0o") || long.TryParse(token, out long _))
                        {
                            output.Append($"{NumberHelper.GetNumber(token)} ");
                        }
                        else
                        {
                            //if token ends with ;, make a new line
                            if (token.EndsWith(";"))
                            {
                                output.AppendLine(token);
                            }
                            else
                            {
                                output.Append($"{token} ");
                            }
                        }

                    }
                }
            }
            //replace all defines
            foreach (var define in PreProcessingDefines)
            {
                //check if it ends in ;, if so, remove it, this is due to an issue with the tokenizer
                if (output.ToString().Contains(define.Key + ";"))
                {
                    output.Replace(define.Key + ";", define.Value);
                }
                output.Replace(define.Key, define.Value);
            }
            //remove each ; at end of line
            foreach (var line in output.ToString().Split('\n'))
            {
                if (line.EndsWith(";"))
                {
                    output.Replace(line, line.Substring(0, line.Length - 1));
                }
            }
            return output.ToString().Replace("{", "").Replace("}", "");
        }
    }

    public static class NumberHelper
    {
        public static long GetNumber(string number)
        {
            if (number.StartsWith("0b"))
            {
                return Convert.ToInt64(number.Substring(2), 2);
            }
            else if (number.StartsWith("0o"))
            {
                return Convert.ToInt64(number.Substring(2), 8);
            }
            else if (number.StartsWith("0x"))
            {
                return Convert.ToInt64(number.Substring(2), 16);
            }
            else if (long.TryParse(number, out long result))
            {
                return result;
            }
            else
            {
                throw new ArgumentException("Invalid number format");
            }
        }
    }
}
