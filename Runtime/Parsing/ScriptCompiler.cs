using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NovelForge.Runtime
{
    public class ScriptCompiler
    {
        private static readonly Regex DialogueLine = new(@"^([A-Za-z_][A-Za-z0-9_]*):\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex SetLine = new(@"^set\s+([A-Za-z_][A-Za-z0-9_]*)\s*(=|\+=|-=)\s*(.+)$", RegexOptions.Compiled);
        private static readonly Regex IfLine = new(@"^if\s+([A-Za-z_][A-Za-z0-9_]*)\s*(==|!=|>=|<=|>|<)\s*(.+)$", RegexOptions.Compiled);

        private readonly CommandRegistry _registry;

        private enum BlockKind { If, Else }

        private struct PendingBlock
        {
            public BlockKind Kind;
            public ConditionalJumpCommand IfCommand;
            public JumpCommand ElseJumpCommand;
        }

        public ScriptCompiler(CommandRegistry registry = null)
        {
            _registry = registry ?? CommandRegistry.CreateDefault();
        }

        public NovelScript Compile(string source)
        {
            var commands = new List<Command>();
            var labels = new Dictionary<string, int>();
            // (commandIndex, targetLabel, choiceOptionIndex-or-null, sourceLineNumber)
            var pendingLabelRefs = new List<(int commandIndex, string labelName, int? choiceOption, int lineNumber)>();
            var blockStack = new Stack<PendingBlock>();
            string pendingComment = null;

            string[] lines = source.Replace("\r\n", "\n").Split('\n');

            for (int lineNumber = 1; lineNumber <= lines.Length; lineNumber++)
            {
                string line = lines[lineNumber - 1].Trim();

                if (line.Length == 0)
                    continue;

                if (line.StartsWith("//"))
                {
                    pendingComment = line.Substring(2).Trim();
                    continue;
                }

                if (line.StartsWith("label ", StringComparison.Ordinal))
                {
                    string name = line.Substring("label ".Length).Trim();
                    if (labels.ContainsKey(name))
                        throw new ParseException(lineNumber, $"Duplicate label '{name}'.");
                    labels[name] = commands.Count;
                    pendingComment = null;
                    continue;
                }

                if (line == "return")
                {
                    commands.Add(Attach(new ReturnCommand(), ref pendingComment));
                    continue;
                }

                if (line.StartsWith("jump ", StringComparison.Ordinal))
                {
                    string target = line.Substring("jump ".Length).Trim();
                    pendingLabelRefs.Add((commands.Count, target, null, lineNumber));
                    commands.Add(Attach(new JumpCommand(-1), ref pendingComment));
                    continue;
                }

                if (line.StartsWith("gosub ", StringComparison.Ordinal))
                {
                    string target = line.Substring("gosub ".Length).Trim();
                    pendingLabelRefs.Add((commands.Count, target, null, lineNumber));
                    commands.Add(Attach(new GosubCommand(-1), ref pendingComment));
                    continue;
                }

                var setMatch = SetLine.Match(line);
                if (setMatch.Success)
                {
                    string varName = setMatch.Groups[1].Value;
                    var op = setMatch.Groups[2].Value switch
                    {
                        "=" => VariableOperator.Assign,
                        "+=" => VariableOperator.Add,
                        "-=" => VariableOperator.Subtract,
                        _ => throw new ParseException(lineNumber, $"Unknown assignment operator '{setMatch.Groups[2].Value}'."),
                    };
                    object value = ParseLiteral(setMatch.Groups[3].Value.Trim(), lineNumber);
                    commands.Add(Attach(new SetVariableCommand(varName, op, value), ref pendingComment));
                    continue;
                }

                var ifMatch = IfLine.Match(line);
                if (ifMatch.Success)
                {
                    string varName = ifMatch.Groups[1].Value;
                    var op = ParseComparisonOperator(ifMatch.Groups[2].Value, lineNumber);
                    object value = ParseLiteral(ifMatch.Groups[3].Value.Trim(), lineNumber);
                    var cmd = new ConditionalJumpCommand(varName, op, value, -1);
                    blockStack.Push(new PendingBlock { Kind = BlockKind.If, IfCommand = cmd });
                    commands.Add(Attach(cmd, ref pendingComment));
                    continue;
                }

                if (line == "else")
                {
                    if (blockStack.Count == 0 || blockStack.Peek().Kind != BlockKind.If)
                        throw new ParseException(lineNumber, "'else' without a matching 'if'.");
                    var pending = blockStack.Pop();
                    var jumpToEndif = new JumpCommand(-1);
                    commands.Add(jumpToEndif);
                    pending.IfCommand.FalseTargetIndex = commands.Count;
                    blockStack.Push(new PendingBlock { Kind = BlockKind.Else, ElseJumpCommand = jumpToEndif });
                    continue;
                }

                if (line == "endif")
                {
                    if (blockStack.Count == 0)
                        throw new ParseException(lineNumber, "'endif' without a matching 'if'.");
                    var pending = blockStack.Pop();
                    if (pending.Kind == BlockKind.If)
                        pending.IfCommand.FalseTargetIndex = commands.Count;
                    else
                        pending.ElseJumpCommand.TargetIndex = commands.Count;
                    continue;
                }

                var dialogueMatch = DialogueLine.Match(line);
                if (dialogueMatch.Success)
                {
                    string characterId = dialogueMatch.Groups[1].Value;
                    (string text, string emotion, string position) = ParseDialogueRest(dialogueMatch.Groups[2].Value);
                    commands.Add(Attach(new SayLineCommand(characterId, text, emotion, position), ref pendingComment));
                    continue;
                }

                // Generic content command: "<name> <args...>" — bg/music/sfx/wait/cg and any
                // custom commands registered on the CommandRegistry passed to this compiler.
                int spaceIndex = line.IndexOf(' ');
                string commandName = spaceIndex < 0 ? line : line.Substring(0, spaceIndex);
                string rawArgs = spaceIndex < 0 ? string.Empty : line.Substring(spaceIndex + 1);
                if (!_registry.TryCreate(commandName, rawArgs, out Command generic))
                    throw new ParseException(lineNumber, $"Unknown command '{commandName}'.");
                commands.Add(Attach(generic, ref pendingComment));
            }

            if (blockStack.Count > 0)
                throw new ParseException(lines.Length, "Unclosed 'if' block — missing 'endif'.");

            foreach (var (commandIndex, labelName, choiceOption, lineNumber) in pendingLabelRefs)
            {
                if (!labels.TryGetValue(labelName, out int targetIndex))
                    throw new ParseException(lineNumber, $"Undefined label '{labelName}'.");

                switch (commands[commandIndex])
                {
                    case JumpCommand jump:
                        jump.TargetIndex = targetIndex;
                        break;
                    case GosubCommand gosub:
                        gosub.TargetIndex = targetIndex;
                        break;
                    case ChoiceCommand choice:
                        choice.ResolveTarget(choiceOption.Value, targetIndex);
                        break;
                }
            }

            return new NovelScript(commands, labels);
        }

        private static Command Attach(Command command, ref string pendingComment)
        {
            command.SourceComment = pendingComment;
            pendingComment = null;
            return command;
        }

        private static (string text, string emotion, string position) ParseDialogueRest(string rest)
        {
            string emotion = null;
            string position = null;
            string text = rest;

            var emotionMatch = Regex.Match(text, @"#(\w+)");
            if (emotionMatch.Success)
            {
                emotion = emotionMatch.Groups[1].Value;
                text = text.Remove(emotionMatch.Index, emotionMatch.Length).TrimEnd();
            }

            // Known limitation: dialogue that legitimately ends in the literal word
            // "left"/"right"/"center" will be misread as a position tag. Acceptable
            // for this DSL's scope — rephrase the line if that ever comes up.
            foreach (string candidate in new[] { "left", "right", "center" })
            {
                var posMatch = Regex.Match(text, $@"\b{candidate}\b$");
                if (posMatch.Success)
                {
                    position = candidate;
                    text = text.Remove(posMatch.Index).TrimEnd();
                    break;
                }
            }

            return (text.Trim(), emotion, position);
        }

        private static ComparisonOperator ParseComparisonOperator(string token, int lineNumber) => token switch
        {
            "==" => ComparisonOperator.Equal,
            "!=" => ComparisonOperator.NotEqual,
            ">" => ComparisonOperator.GreaterThan,
            ">=" => ComparisonOperator.GreaterOrEqual,
            "<" => ComparisonOperator.LessThan,
            "<=" => ComparisonOperator.LessOrEqual,
            _ => throw new ParseException(lineNumber, $"Unknown comparison operator '{token}'."),
        };

        private static object ParseLiteral(string token, int lineNumber)
        {
            if (token == "true") return true;
            if (token == "false") return false;
            if (token.StartsWith("\"") && token.EndsWith("\"") && token.Length >= 2)
                return token.Substring(1, token.Length - 2);
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                return i;
            if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                return f;
            throw new ParseException(lineNumber, $"Could not parse value '{token}' — expected true/false, a number, or a \"quoted string\".");
        }
    }
}
