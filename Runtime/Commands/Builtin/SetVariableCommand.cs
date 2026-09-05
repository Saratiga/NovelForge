using System;
using System.Collections;

namespace NovelForge.Runtime
{
    public enum VariableOperator { Assign, Add, Subtract }

    public class SetVariableCommand : Command
    {
        private readonly string _variableName;
        private readonly VariableOperator _op;
        private readonly object _value;

        public SetVariableCommand(string variableName, VariableOperator op, object value)
        {
            _variableName = variableName;
            _op = op;
            _value = value;
        }

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            switch (_op)
            {
                case VariableOperator.Assign:
                    context.Variables.Set(_variableName, _value);
                    break;
                case VariableOperator.Add:
                    // Arithmetic always normalizes to float internally; GetInt() still
                    // works afterwards because Convert.ToInt32 accepts a float source.
                    context.Variables.Set(_variableName, context.Variables.GetFloat(_variableName) + Convert.ToSingle(_value));
                    break;
                case VariableOperator.Subtract:
                    context.Variables.Set(_variableName, context.Variables.GetFloat(_variableName) - Convert.ToSingle(_value));
                    break;
            }
            yield break;
        }
    }
}
