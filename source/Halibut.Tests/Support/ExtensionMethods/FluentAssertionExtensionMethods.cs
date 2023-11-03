using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Primitives;

namespace Halibut.Tests.Support.ExtensionMethods
{
    public static class FluentAssertionExtensionMethods
    {
        public static AndConstraint<ObjectAssertions> BeTaskOrOperationCancelledException(this ObjectAssertions should)
        {
            return should.Match(x => x is TaskCanceledException || x is OperationCanceledException || IsHalibutWrappedOperationCancelledException(x as HalibutClientException));
        }

        static readonly Regex OperationCancelledRegex = new("The [a-zA-Z]* operation was cancelled", RegexOptions.Compiled);

        static bool IsHalibutWrappedOperationCancelledException(HalibutClientException? ex)
        {
            if (ex is null)
            {
                return false;
            }

            if (OperationCancelledRegex.IsMatch(ex.Message))
            {
                return true;
            }

            return false;
        }
    }
}