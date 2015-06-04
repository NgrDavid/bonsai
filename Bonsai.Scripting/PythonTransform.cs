﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using Microsoft.Scripting.Hosting;
using System.Drawing.Design;
using Bonsai.Expressions;
using System.Reactive.Linq;
using System.Linq.Expressions;
using IronPython.Runtime;

namespace Bonsai.Scripting
{
    [DefaultProperty("Script")]
    [WorkflowElementCategory(ElementCategory.Transform)]
    [Description("A Python script used to process and convert individual elements of the input sequence.")]
    public class PythonTransform : SingleArgumentExpressionBuilder
    {
        public PythonTransform()
        {
            Script = "@returns(bool)\ndef process(value):\n  return True";
        }

        [Editor(typeof(PythonScriptEditor), typeof(UITypeEditor))]
        [Description("The script that determines the operation of the transform.")]
        public string Script { get; set; }

        public override Expression Build(IEnumerable<Expression> arguments)
        {
            var engine = IronPython.Hosting.Python.CreateEngine();
            var scope = engine.CreateScope();
            var script = PythonHelper.ReturnsDecorator + Script;
            var scriptSource = engine.CreateScriptSourceFromString(script);
            scriptSource.Execute(scope);

            object transform;
            var source = arguments.Single();
            var observableType = source.Type.GetGenericArguments()[0];
            if (PythonHelper.TryGetClass(scope, "Transform", out transform))
            {
                var classExpression = Expression.Constant(transform);
                var opExpression = Expression.Constant(engine.Operations);
                var outputType = PythonHelper.GetOutputType(engine.Operations, transform, PythonHelper.ProcessFunction);
                return Expression.Call(
                    typeof(PythonTransform),
                    "Process",
                    new[] { observableType, outputType },
                    source,
                    opExpression,
                    classExpression);
            }
            else
            {
                var outputType = PythonHelper.GetOutputType(scope, PythonHelper.ProcessFunction);
                var scopeExpression = Expression.Constant(scope);
                return Expression.Call(
                    typeof(PythonTransform),
                    "Process",
                    new[] { observableType, outputType },
                    source,
                    scopeExpression);
            }
        }

        static IObservable<TResult> Process<TSource, TResult>(
            IObservable<TSource> source,
            ObjectOperations op,
            object processorClass)
        {
            return Observable.Defer(() =>
            {
                var processor = new PythonProcessor<TSource, TResult>(op, processorClass);
                var result = source.Select(processor.Process);
                if (processor.Load != null) processor.Load();
                if (processor.Unload != null)
                {
                    return result.Finally(processor.Unload);
                }
                else return result;
            });
        }

        static IObservable<TResult> Process<TSource, TResult>(
            IObservable<TSource> source,
            ScriptScope scope)
        {
            var processor = new PythonProcessor<TSource, TResult>(scope);
            var result = source.Select(processor.Process);
            if (processor.Unload != null)
            {
                result = result.Finally(processor.Unload);
            }

            if (processor.Load != null)
            {
                var observable = result;
                result = Observable.Defer(() =>
                {
                    processor.Load();
                    return observable;
                });
            }

            return result;
        }
    }
}
