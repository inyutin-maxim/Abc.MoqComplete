﻿using System.Linq;
using Abc.MoqComplete.Extensions;
using Abc.MoqComplete.Services;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.CodeCompletion;
using JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure.LookupItems;
using JetBrains.ReSharper.Feature.Services.CSharp.CodeCompletion.Infrastructure;
using JetBrains.ReSharper.Features.Intellisense.CodeCompletion.CSharp;
using JetBrains.ReSharper.Features.Intellisense.CodeCompletion.CSharp.Rules;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.ExpectedTypes;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Resources;
using JetBrains.ReSharper.Psi.Tree;

namespace Abc.MoqComplete.CompletionProvider.ItIsAny
{
	public abstract class BaseItIsAnyParameterProvider : CSharpItemsProviderBase<CSharpCodeCompletionContext>
	{
		protected abstract bool IsSetupMethod(IMoqMethodIdentifier identifier, IInvocationExpression methodInvocation);
		protected abstract bool IsVerifyMethod(IMoqMethodIdentifier identifier, IInvocationExpression methodInvocation);

		protected override bool IsAvailable(CSharpCodeCompletionContext context)
		{
			var codeCompletionType = context.BasicContext.CodeCompletionType;

			return codeCompletionType == CodeCompletionType.BasicCompletion || codeCompletionType == CodeCompletionType.SmartCompletion;
		}

		protected override bool AddLookupItems(CSharpCodeCompletionContext context, IItemsCollector collector)
		{
			var identifier = context.TerminatedContext.TreeNode as IIdentifier;
			var mockedMethodArgument = identifier.GetParentSafe<IReferenceExpression>().GetParentSafe<ICSharpArgument>();

			var mockedMethodInvocationExpression =
				mockedMethodArgument?.GetParentSafe<IArgumentList>().GetParentSafe<IInvocationExpression>();

			var methodInvocation = mockedMethodInvocationExpression?.GetParentSafe<ILambdaExpression>()
				.GetParentSafe<IArgument>()
				.GetParentSafe<IArgumentList>()
				.GetParentSafe<IInvocationExpression>();

			if (methodInvocation == null)
			{
				return false;
			}

			var methodIdentifier = context.BasicContext.Solution.GetComponent<IMoqMethodIdentifier>();
			var isSetup = IsSetupMethod(methodIdentifier, methodInvocation);
            var isVerify = IsVerifyMethod(methodIdentifier, methodInvocation);

			if (!isSetup && !isVerify)
			{
				return false;
			}

			var argumentIndex = mockedMethodArgument.IndexOf();

			if (context.ExpectedTypesContext != null)
			{
				foreach (var expectedType in context.ExpectedTypesContext.ExpectedITypes)
				{
					if (expectedType.Type == null)
					{
						continue;
					}

					var typeName = expectedType.Type.GetPresentableName(CSharpLanguage.Instance);
					var proposedCompletion = context.IsQualified ? $"IsAny<{typeName}>()" : $"It.IsAny<{typeName}>()";
					AddLookup(context, collector, proposedCompletion);
				}
			}

			if (argumentIndex != 0 || mockedMethodInvocationExpression.Reference == null || context.IsQualified)
			{
				return true;
			}

			var candidates = mockedMethodInvocationExpression.InvocationExpressionReference.GetCandidates();
			foreach (var candidate in candidates)
            {
                var method = candidate.GetDeclaredElement() as IMethod;
                var substitution = candidate.GetSubstitution();

				if (method == null || method.Parameters.Count <= 1)
					continue;

                var parameter = method.Parameters.Select(x => GetItIsAny(x, substitution));
                var proposedCompletion = string.Join(", ", parameter);
                AddLookup(context, collector, proposedCompletion, isSetup ? 2 : 1);
			}

			return false;
		}

		private static string GetItIsAny(IParameter x, ISubstitution substitution)
		{
			if (substitution == null)
			{
				return "It.IsAny<" + x.Type.GetPresentableName(CSharpLanguage.Instance) + ">()";
			}

			return "It.IsAny<" + substitution.Apply(x.Type).GetPresentableName(CSharpLanguage.Instance) + ">()";
		}

		private static void AddLookup(CSharpCodeCompletionContext context, IItemsCollector collector, string proposedCompletion,
									int? offset = null)
		{
			var textLookupItem =
				CSharpLookupItemFactory.Instance.CreateKeywordLookupItem(context,
					proposedCompletion,
					TailType.None,
					PsiSymbolsThemedIcons.Method.Id);

			if (offset != null)
			{
				textLookupItem.SetInsertCaretOffset(offset.Value);
				textLookupItem.SetReplaceCaretOffset(offset.Value);
			}

			textLookupItem.SetTopPriority();
			collector.Add(textLookupItem);
		}
	}
}