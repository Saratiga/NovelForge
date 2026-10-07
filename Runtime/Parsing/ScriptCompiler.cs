using System;
using System.Collections.Generic;
using System.Globalization;

namespace NovelForge.Runtime
{
    public class ScriptCompiler
    {
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

            // Localization: every dialogue line and choice option gets a stable id, either
            // explicit (@tag) or auto-generated as "<nearest label>_<ordinal>". usedIds
            // enforces global uniqueness across the whole script (the translation table on
            // disk is one flat dictionary); currentLabel/lineIdOrdinal track auto-id state.
            var usedIds = new HashSet<string>();
            string currentLabel = "_start";
            int lineIdOrdinal = 0;

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

                if (DslGrammar.TryParseLabel(line, out string name))
                {
                    if (labels.ContainsKey(name))
                        throw new ParseException(lineNumber, $"Duplicate label '{name}'.");
                    labels[name] = commands.Count;
                    currentLabel = name;
                    lineIdOrdinal = 0;
                    pendingComment = null;
                    continue;
                }

                if (line == "return")
                {
                    commands.Add(Attach(new ReturnCommand(), ref pendingComment));
                    continue;
                }

                if (DslGrammar.TryParseJump(line, out string target, out bool isGosub))
                {
                    pendingLabelRefs.Add((commands.Count, target, null, lineNumber));
                    Command jump = isGosub ? new GosubCommand(-1) : new JumpCommand(-1);
                    commands.Add(Attach(jump, ref pendingComment));
                    continue;
                }

                var setMatch = DslGrammar.SetLine.Match(line);
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

                var ifMatch = DslGrammar.IfLine.Match(line);
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

                if (line == "choice")
                {
                    var options = new List<(string text, string id, string labelName)>();
                    int lookahead = lineNumber;
                    while (lookahead < lines.Length)
                    {
                        string nextLine = lines[lookahead].Trim();
                        if (nextLine.Length == 0 || nextLine.StartsWith("//"))
                        {
                            lookahead++;
                            continue;
                        }
                        var optionMatch = DslGrammar.ChoiceOptionLine.Match(nextLine);
                        if (!optionMatch.Success)
                            break;
                        string explicitOptionId = optionMatch.Groups[2].Success ? optionMatch.Groups[2].Value : null;
                        options.Add((optionMatch.Groups[1].Value, explicitOptionId, optionMatch.Groups[3].Value));
                        lookahead++;
                    }
                    if (options.Count == 0)
                        throw new ParseException(lineNumber, "'choice' has no options.");

                    var optionIds = new string[options.Count];
                    for (int i = 0; i < options.Count; i++)
                        optionIds[i] = AllocateId(options[i].id, ref lineIdOrdinal, currentLabel, usedIds, lineNumber);

                    var choiceCommand = new ChoiceCommand(options.ConvertAll(o => o.text), optionIds, options.Count);
                    for (int i = 0; i < options.Count; i++)
                        pendingLabelRefs.Add((commands.Count, options[i].labelName, i, lineNumber));
                    commands.Add(Attach(choiceCommand, ref pendingComment));

                    // lines[lineNumber .. lookahead-1] (0-indexed) were option lines already consumed;
                    // jump the 1-based cursor to lookahead so the next loop iteration (which does
                    // lineNumber++) resumes at lines[lookahead], the first unconsumed line.
                    lineNumber = lookahead;
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

                var dialogueMatch = DslGrammar.DialogueLine.Match(line);
                if (dialogueMatch.Success)
                {
                    string characterId = dialogueMatch.Groups[1].Value;
                    (string text, string emotion, string position, string explicitId) = ParseDialogueRest(dialogueMatch.Groups[2].Value);
                    string lineId = AllocateId(explicitId, ref lineIdOrdinal, currentLabel, usedIds, lineNumber);
                    commands.Add(Attach(new SayLineCommand(characterId, text, emotion, position, lineId), ref pendingComment));
                    continue;
                }

                // Generic content command: "<name> <args...>" — bg/music/sfx/wait/cg and any
                // custom commands registered on the CommandRegistry passed to this compiler.
                int spaceIndex = line.IndexOf(' ');
                string commandName = spaceIndex < 0 ? line : line.Substring(0, spaceIndex);
                string rawArgs = spaceIndex < 0 ? string.Empty : line.Substring(spaceIndex + 1);
                Command generic;
                try
                {
                    if (!_registry.TryCreate(commandName, rawArgs, out generic))
                        throw new ParseException(lineNumber, $"Unknown command '{commandName}'.");
                }
                catch (Exception e) when (e is not ParseException)
                {
                    // A registered factory (e.g. "wait"'s float.Parse) can throw its own
                    // exception type on bad arguments — re-raise as a located ParseException
                    // so every parse failure, built-in or custom, carries a line number.
                    throw new ParseException(lineNumber, $"Command '{commandName}' rejected arguments '{rawArgs}': {e.Message}");
                }
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

        private static string AllocateId(string explicitId, ref int ordinal, string currentLabel, HashSet<string> usedIds, int lineNumber)
        {
            ordinal++;
            string id = explicitId ?? $"{currentLabel}_{ordinal}";
            if (!usedIds.Add(id))
                throw new ParseException(lineNumber, $"Duplicate localization id '{id}'.");
            return id;
        }

        private static (string text, string emotion, string position, string id) ParseDialogueRest(string rest)
        {
            string emotion = null;
            string position = null;
            string id = null;
            string text = rest;

            var emotionMatch = DslGrammar.EmotionTag.Match(text);
            if (emotionMatch.Success)
            {
                emotion = emotionMatch.Groups[1].Value;
                text = text.Remove(emotionMatch.Index, emotionMatch.Length).TrimEnd();
            }

            var idMatch = DslGrammar.IdTag.Match(text);
            if (idMatch.Success)
            {
                id = idMatch.Groups[1].Value;
                text = text.Remove(idMatch.Index, idMatch.Length).TrimEnd();
            }

            // Known limitation: dialogue that legitimately ends in the literal word
            // "left"/"right"/"center" will be misread as a position tag. Acceptable
            // for this DSL's scope — rephrase the line if that ever comes up.
            var posMatch = DslGrammar.TrailingPosition.Match(text);
            if (posMatch.Success)
            {
                position = posMatch.Groups[1].Value;
                text = text.Remove(posMatch.Index).TrimEnd();
            }

            return (text.Trim(), emotion, position, id);
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
