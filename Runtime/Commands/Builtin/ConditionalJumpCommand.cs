using System;
using System.Collections;

namespace NovelForge.Runtime
{
    public enum ComparisonOperator { Equal, NotEqual, GreaterThan, GreaterOrEqual, LessThan, LessOrEqual }

    public class ConditionalJumpCommand : Command
    {
        private readonly string _variableName;
        private readonly ComparisonOperator _op;
        private readonly object _value;

        public int FalseTargetIndex { get; internal set; }

        public ConditionalJumpCommand(string variableName, ComparisonOperator op, object value, int falseTargetIndex)
        {
            _variableName = variableName;
            _op = op;
            _value = value;
            FalseTargetIndex = falseTargetIndex;
        }

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            if (!Evaluate(context.Variables))
                pointer.Current = FalseTargetIndex;
            yield break;
        }

        private bool Evaluate(VariableStore variables)
        {
            if (_value is string s)
            {
                string current = variables.GetString(_variableName);
                return _op switch
                {
                    ComparisonOperator.Equal => current == s,
                    ComparisonOperator.NotEqual => current != s,
                    _ => throw new InvalidOperationException($"Operator {_op} is not supported for string comparisons."),
                };
            }

            if (_value is bool b)
            {
                bool current = variables.GetBool(_variableName);
                return _op switch
                {
                    ComparisonOperator.Equal => current == b,
                    ComparisonOperator.NotEqual => current != b,
                    _ => throw new InvalidOperationException($"Operator {_op} is not supported for bool comparisons."),
                };
            }

            float currentNumber = variables.GetFloat(_variableName);
            float compareNumber = Convert.ToSingle(_value);
            return _op switch
            {
                ComparisonOperator.Equal => currentNumber == compareNumber,
                ComparisonOperator.NotEqual => currentNumber != compareNumber,
                ComparisonOperator.GreaterThan => currentNumber > compareNumber,
                ComparisonOperator.GreaterOrEqual => currentNumber >= compareNumber,
                ComparisonOperator.LessThan => currentNumber < compareNumber,
                ComparisonOperator.LessOrEqual => currentNumber <= compareNumber,
                _ => throw new InvalidOperationException($"Unhandled operator {_op}."),
            };
        }
    }
}
