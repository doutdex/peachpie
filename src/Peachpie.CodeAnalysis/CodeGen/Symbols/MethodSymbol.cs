﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.CodeAnalysis;
using Pchp.CodeAnalysis.CodeGen;

namespace Pchp.CodeAnalysis.Symbols
{
    internal abstract partial class MethodSymbol
    {
        static TypeSymbol EmitCreateRoutine(CodeGenerator cg, MethodSymbol method)
        {
            var overloads = ImmutableArray<MethodSymbol>.Empty;

            if (method is AmbiguousMethodSymbol a)
            {
                Debug.Assert(a.IsOverloadable);
                method = a.Ambiguities[0];
                overloads = a.Ambiguities.RemoveAt(0);
            }

            var il = cg.Builder;

            il.EmitStringConstant(method.MetadataName);
            cg.EmitLoadToken(method, null);
            cg.Emit_NewArray(cg.CoreTypes.RuntimeMethodHandle, overloads, m => cg.EmitLoadToken(m, null));

            return cg.EmitCall(ILOpCode.Call, cg.CoreMethods.Reflection.CreateUserRoutine_string_RuntimeMethodHandle_RuntimeMethodHandleArr);
        }

        /// <summary>
        /// Emits load of cached <c>RoutineInfo</c> corresponding to this method.
        /// </summary>
        /// <returns>Type symbol of <c>RoutineInfo</c>.</returns>
        internal virtual TypeSymbol EmitLoadRoutineInfo(CodeGenerator cg)
        {
            var name = MetadataName;
            var type = this.ContainingType;
            name = (type != null ? type.GetFullName() : "?") + "." + name; 

            // cache the instance of RoutineInfo
            var tmpfld = cg.Module.SynthesizedManager.GetOrCreateSynthesizedField(
                cg.Module.ScriptType,
                cg.CoreTypes.RoutineInfo,
                "<>" + name,
                Accessibility.Internal,
                isstatic: true,
                @readonly: false);

            // Template: (tmpfld ?? tmpfld = CreateUserRoutine)
            var tmpplace = new FieldPlace(null, tmpfld, cg.Module);
            tmpplace.EmitLoad(cg.Builder);
            cg.EmitNullCoalescing((cg_) =>
            {
                // TODO: Interlocked(ref fld, CreateRoutine, null)
                tmpplace.EmitStorePrepare(cg_.Builder);
                EmitCreateRoutine(cg_, this);

                cg_.Builder.EmitOpCode(ILOpCode.Dup);
                tmpplace.EmitStore(cg_.Builder);
            });

            //
            return tmpfld.Type
                .Expect(cg.CoreTypes.RoutineInfo);
        }
    }
}
