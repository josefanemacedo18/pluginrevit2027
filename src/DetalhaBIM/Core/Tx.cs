using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DetalhaBIM.Core
{
    /// <summary>
    /// Auxiliares de transação. Todas as transações do plugin descartam avisos (warnings)
    /// automaticamente para não interromper processamentos em lote, mas preservam erros.
    /// </summary>
    public static class Tx
    {
        /// <param name="deleteFailingAnnotations">
        /// Quando verdadeiro, erros que o Revit resolve excluindo elementos (ex.: uma cota com
        /// referência inválida) são resolvidos automaticamente, em vez de desfazer todo o lote.
        /// Usado apenas em transações que criam anotações.
        /// </param>
        public static void Run(Document doc, string name, Action action, bool deleteFailingAnnotations = false)
        {
            using (var t = new Transaction(doc, "DetalhaBIM - " + name))
            {
                Configure(t, deleteFailingAnnotations);
                t.Start();
                action();
                if (t.GetStatus() == TransactionStatus.Started) t.Commit();
            }
        }

        public static T Run<T>(Document doc, string name, Func<T> action)
        {
            T result = default;
            Run(doc, name, () => { result = action(); });
            return result;
        }

        public static void Configure(Transaction t, bool deleteFailingAnnotations = false)
        {
            FailureHandlingOptions options = t.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new WarningSwallower(deleteFailingAnnotations));
            options.SetClearAfterRollback(true);
            t.SetFailureHandlingOptions(options);
        }

        /// <summary>
        /// Executa uma ação dentro de uma sub-transação; se ela lançar exceção, desfaz somente
        /// essa parte e retorna false, preservando o restante do trabalho.
        /// </summary>
        public static bool TrySub(Document doc, Action action)
        {
            using (var st = new SubTransaction(doc))
            {
                st.Start();
                try
                {
                    action();
                    st.Commit();
                    return true;
                }
                catch
                {
                    if (st.GetStatus() == TransactionStatus.Started) st.RollBack();
                    return false;
                }
            }
        }
    }

    /// <summary>Remove avisos (não erros) gerados durante as transações do plugin.</summary>
    public class WarningSwallower : IFailuresPreprocessor
    {
        private readonly bool _deleteFailingAnnotations;

        public WarningSwallower(bool deleteFailingAnnotations = false)
        {
            _deleteFailingAnnotations = deleteFailingAnnotations;
        }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            bool resolved = false;
            IList<FailureMessageAccessor> messages = accessor.GetFailureMessages();
            foreach (FailureMessageAccessor message in messages)
            {
                FailureSeverity severity = message.GetSeverity();
                if (severity == FailureSeverity.Warning)
                {
                    accessor.DeleteWarning(message);
                }
                else if (_deleteFailingAnnotations && severity == FailureSeverity.Error && message.HasResolutions()
                         && message.HasResolutionOfType(FailureResolutionType.DeleteElements)
                         && message.GetFailingElementIds().All(id => IsAnnotation(accessor.GetDocument(), id)))
                {
                    message.SetCurrentResolutionType(FailureResolutionType.DeleteElements);
                    accessor.ResolveFailure(message);
                    resolved = true;
                }
            }
            return resolved ? FailureProcessingResult.ProceedWithCommit : FailureProcessingResult.Continue;
        }

        private static bool IsAnnotation(Document doc, ElementId id)
        {
            Element e = doc.GetElement(id);
            return e == null || e is Dimension || e is IndependentTag || e is SpatialElementTag
                   || e.Category?.CategoryType == CategoryType.Annotation;
        }
    }
}
