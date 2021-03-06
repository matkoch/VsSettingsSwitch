﻿// Copyright 2016, 2015, 2014 Matthias Koch
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using System.Linq;
using Coconut.Utilities;
using EnvDTE;
using JetBrains.Annotations;
using JetBrains.Application.ComponentModel;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.Navigation.ContextNavigation;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.DataContext;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.Util;

namespace Coconut.Debugging.GotoDeclaration
{
    // TODO: C#6 + Nullability showcase
    [ShellFeaturePart]
    public class DebugDeclarationSearch : DefaultDeclarationSearch
    {
        public override bool IsContextApplicable ([NotNull] IDataContext context)
        {
            return DebuggingService.IsDebugging;
        }

        protected override IEnumerable<DeclaredElementTypeUsageInfo> GetCandidates ([NotNull] IDataContext context, ReferencePreferenceKind kind)
        {
            var actualDeclaredElement = GetActualDeclaredElement(context);
            if (actualDeclaredElement != null)
                return new[] { new DeclaredElementTypeUsageInfo(actualDeclaredElement, isDeclaration: true) };

            return EmptyList<DeclaredElementTypeUsageInfo>.InstanceList;
        }

        [CanBeNull]
        private ITypeMember GetActualDeclaredElement (IDataContext context)
        {
            var expression = DebuggingService.GetInitializedExpression(context);
            if (expression == null)
                return null;

            var type = GetTypeFullName(context, expression);
            return GetDeclaredElement(context, type);
        }

        private static string GetTypeFullName (IDataContext context, Expression expression)
        {
            var type = expression.NotNull().Type;
            type = type.Substring(type.IndexOf(value: '{') + 1);
            type = type.Substring(startIndex: 0, length: type.Length - 1);

            var keywordsService = LanguageManager.Instance.TryGetService<ITypeKeywordsService>(context.GetSourceFile().NotNull().PrimaryPsiLanguage);
            if (keywordsService != null)
                type = keywordsService.GetFullQualifiedTypeName(type) ?? type;

            return type;
        }

        [CanBeNull]
        private static ITypeMember GetDeclaredElement (IDataContext context, string actualType)
        {
            var declaredType = TypeFactory.CreateTypeByCLRName(actualType, context.GetSourceFile().NotNull().PsiModule);
            var resolveResult = declaredType.Resolve();
            if (!resolveResult.IsValid() || resolveResult.IsEmpty)
                return null;

            var declaredElement = (ITypeElement) resolveResult.DeclaredElement;
            if (declaredElement == null)
                return null;

            var selectedElement = context.GetDeclaredElements().Single();
            var matchingMembers = declaredElement.GetMembers().Where(x => IsMatchingMember(x, selectedElement)).ToList();
            return matchingMembers.FirstOrDefault();
        }

        private static bool IsMatchingMember (IDeclaredElement candidate, IDeclaredElement selectedElement)
        {
            if (candidate.ShortName != selectedElement.ShortName)
                return false;

            if (candidate.Equals(selectedElement))
                return true;

            var overridableMember = candidate as IOverridableMember;
            if (overridableMember == null)
                return true;

            var superMembers = overridableMember.GetAllSuperMembers().Select(y => y.Element);
            if (superMembers.Contains(selectedElement))
                return true;

            return false;
        }
    }
}