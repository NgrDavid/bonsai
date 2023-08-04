﻿using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reactive.Linq;
using System.Collections.Generic;
using System.Reactive;

namespace Bonsai.Core.Tests
{
    public partial class CombinatorBuilderTests
    {
        [Combinator]
        class OverloadedCombinatorMock
        {
            public IObservable<float> Process(IObservable<float> source)
            {
                return source;
            }

            public IObservable<double> Process(IObservable<double> source)
            {
                return source;
            }
        }

        [Combinator]
        class ParamsOverloadedCombinatorMock
        {
            public IObservable<float> Process(params IObservable<float>[] source)
            {
                return source.FirstOrDefault();
            }

            public IObservable<double> Process(params IObservable<double>[] source)
            {
                return source.FirstOrDefault();
            }
        }

        [Combinator]
        class GenericOverloadedCombinatorMock
        {
            public IObservable<float> Process(IObservable<float> source)
            {
                return source.Select(x => x + 1);
            }

            public IObservable<TSource> Process<TSource>(IObservable<TSource> source)
            {
                return source;
            }
        }

        [Combinator]
        class ListTupleOverloadedCombinatorMock
        {
            public IObservable<int> Process(IObservable<Tuple<int, int>> source)
            {
                return source.Select(xs => xs.Item1);
            }

            public IObservable<IList<int>> Process(IObservable<IList<int>> source)
            {
                return source;
            }
        }

        [Combinator]
        class AmbiguousOverloadedCombinatorMock
        {
            public IObservable<int> Process(IObservable<int> source1, IObservable<double> source2)
            {
                return source1;
            }

            public IObservable<int> Process(IObservable<double> source1, IObservable<int> source2)
            {
                return source2;
            }

            public IObservable<int> Process(IObservable<object> source1, IObservable<object> source2)
            {
                return null;
            }
        }

        [Combinator]
        class SpecializedGenericOverloadedCombinatorMock
        {
            public IObservable<TSource> Process<TSource>(IObservable<TSource> source)
            {
                return source;
            }

            public IObservable<TSource> Process<TSource>(IObservable<Timestamped<TSource>> source)
            {
                return source.Select(x => x.Value);
            }
        }

        class HidingOverloadedCombinatorMock : OverloadedCombinatorMock
        {
            public new IObservable<double> Process(IObservable<double> source)
            {
                return source.Select(x => double.NaN);
            }
        }

        class HidingSpecializedGenericOverloadedCombinatorMock : SpecializedGenericOverloadedCombinatorMock
        {
            public new IObservable<TSource> Process<TSource>(IObservable<Timestamped<TSource>> source)
            {
                return source.Select(x => default(TSource));
            }
        }

        [Combinator]
        class BaseVirtualCombinatorMock
        {
            public virtual IObservable<string> Process(IObservable<string> source) => source;
        }

        class DerivedOverrideCombinatorMock : BaseVirtualCombinatorMock
        {
            public override IObservable<string> Process(IObservable<string> source) => Observable.Return(string.Empty);
        }

        class DerivedOverrideOverloadedCombinatorMock : BaseVirtualCombinatorMock
        {
            public override IObservable<string> Process(IObservable<string> source) => source;

            public IObservable<object> Process(IObservable<object> _) => Observable.Return(default(object));
        }

        [Combinator]
        abstract class BaseGenericOverloadedCombinatorMock
        {
            public abstract IObservable<TSource> Process<TSource>(IObservable<TSource> source);
        }

        class DerivedOverrideGenericOverloadedCombinatorMock : BaseGenericOverloadedCombinatorMock
        {
            public override IObservable<TSource> Process<TSource>(IObservable<TSource> source) => source;

            public IObservable<EventArgs> Process(IObservable<EventArgs> _)
                => Observable.Return(default(EventArgs));
        }

        class DerivedOverridePrimitiveTransformMock : Transform<double, double>
        {
            public override IObservable<double> Process(IObservable<double> source) => source;

            public IObservable<decimal> Process(IObservable<decimal> source) => source;
        }

        [TestMethod]
        public void Build_DoubleOverloadedMethodCalledWithDouble_ReturnsDoubleValue()
        {
            var value = 5.0;
            var combinator = new OverloadedCombinatorMock();
            var source = CreateObservableExpression(Observable.Return(value));
            var resultProvider = TestCombinatorBuilder<double>(combinator, source);
            var result = Last(resultProvider).Result;
            Assert.AreEqual(value, result);
        }

        [TestMethod]
        public void Build_FloatOverloadedMethodCalledWithInt_ReturnsFloatValue()
        {
            var value = 5;
            var combinator = new OverloadedCombinatorMock();
            var source = CreateObservableExpression(Observable.Return(value));
            var resultProvider = TestCombinatorBuilder<float>(combinator, source);
            var result = Last(resultProvider).Result;
            Assert.AreEqual(value, result);
        }

        [TestMethod]
        public void Build_ParamsFloatOverloadedMethodCalledWithInt_ReturnsFloatValue()
        {
            var value = 5;
            var combinator = new ParamsOverloadedCombinatorMock();
            var source = CreateObservableExpression(Observable.Return(value));
            var resultProvider = TestCombinatorBuilder<float>(combinator, source);
            var result = Last(resultProvider).Result;
            Assert.AreEqual(value, result);
        }

        [TestMethod]
        public void Build_GenericFloatOverloadedMethodCalledWithFloat_ReturnsFloatValue()
        {
            var value = 5.0f;
            var combinator = new GenericOverloadedCombinatorMock();
            var source = CreateObservableExpression(Observable.Return(value));
            var resultProvider = TestCombinatorBuilder<float>(combinator, source);
            var result = Last(resultProvider).Result;
            Assert.AreEqual(value + 1, result);
        }

        [TestMethod]
        public void Build_ListTupleOverloadedMethodCalledWithIntTuple_ReturnsIntValue()
        {
            var value = 5;
            var combinator = new ListTupleOverloadedCombinatorMock();
            var source = CreateObservableExpression(Observable.Return(Tuple.Create(value, value)));
            var resultProvider = TestCombinatorBuilder<int>(combinator, source);
            var result = Last(resultProvider).Result;
            Assert.AreEqual(value, result);
        }

        [TestMethod]
        [ExpectedException(typeof(WorkflowBuildException))]
        public void Build_AmbiguousOverloadedMethodCalledWithIntTuple_ThrowsWorkflowBuildException()
        {
            var value = 5;
            var combinator = new AmbiguousOverloadedCombinatorMock();
            var source1 = CreateObservableExpression(Observable.Return(value));
            var source2 = CreateObservableExpression(Observable.Return(value));
            var resultProvider = TestCombinatorBuilder<int>(combinator, source1, source2);
            var result = Last(resultProvider).Result;
            Assert.AreEqual(value, result);
        }

        [TestMethod]
        public void Build_AverageOverloadedMethodCalledWithLong_ReturnsDoubleValue()
        {
            var value = 5L;
            var combinator = new Bonsai.Reactive.Average();
            var source = CreateObservableExpression(Observable.Return(value));
            var resultProvider = TestCombinatorBuilder<double>(combinator, source);
            var result = Last(resultProvider).Result;
            Assert.AreEqual(value, result);
        }

        [TestMethod]
        public void Build_SpecializedGenericOverloadedMethod_ReturnsValue()
        {
            var value = 5;
            var combinator = new SpecializedGenericOverloadedCombinatorMock();
            var source = CreateObservableExpression(Observable.Return(value).Timestamp());
            var resultProvider = TestCombinatorBuilder<int>(combinator, source);
            var result = Last(resultProvider).Result;
            Assert.AreEqual(value, result);
        }

        [TestMethod]
        public void Build_HidingDoubleOverloadedMethodCalledWithDouble_ReturnsDoubleValue()
        {
            var value = 5.0;
            var combinator = new HidingOverloadedCombinatorMock();
            var source = CreateObservableExpression(Observable.Return(value));
            var resultProvider = TestCombinatorBuilder<double>(combinator, source);
            var result = Last(resultProvider).Result;
            Assert.AreNotEqual(value, result);
        }

        [TestMethod]
        public void Build_HidingSpecializedGenericOverloadedMethod_ReturnsValue()
        {
            var value = 5;
            var combinator = new HidingSpecializedGenericOverloadedCombinatorMock();
            var source = CreateObservableExpression(Observable.Return(value).Timestamp());
            var resultProvider = TestCombinatorBuilder<int>(combinator, source);
            var result = Last(resultProvider).Result;
            Assert.AreNotEqual(value, result);
        }

        [TestMethod]
        public void Build_DerivedOverrideMethodCalledWithString_ReturnsOverrideValue()
        {
            var value = "5";
            var combinator = new DerivedOverrideCombinatorMock();
            var source = CreateObservableExpression(Observable.Return(value));
            var resultProvider = TestCombinatorBuilder<string>(combinator, source);
            var result = Last(resultProvider).Result;
            Assert.AreNotEqual(value, result);
        }

        [TestMethod]
        public void Build_DerivedOverrideOverloadedMethodCalledWithString_ReturnsObjectValue()
        {
            var value = "5";
            var combinator = new DerivedOverrideOverloadedCombinatorMock();
            var source = CreateObservableExpression(Observable.Return(value));
            var resultProvider = TestCombinatorBuilder<object>(combinator, source);
            var result = Last(resultProvider).Result;
            Assert.AreNotEqual(value, result);
        }

        [TestMethod]
        public void Build_OverloadOverrideWithRefTypeAndCallWithObject_ReturnsObjectValue()
        {
            var value = new object();
            var combinator = new DerivedOverrideGenericOverloadedCombinatorMock();
            var source = CreateObservableExpression(Observable.Return(value));
            var resultProvider = TestCombinatorBuilder<object>(combinator, source);
            var result = Last(resultProvider).Result;
            Assert.AreNotEqual(value, result);
        }

        [TestMethod]
        public void Build_OverloadOverrideCalledWithConvertibleValue_ReturnsOriginalTypeValue()
        {
            var value = 5.0;
            var combinator = new DerivedOverridePrimitiveTransformMock();
            var source = CreateObservableExpression(Observable.Return(value));
            var resultProvider = TestCombinatorBuilder<double>(combinator, source);
            var result = Last(resultProvider).Result;
            Assert.AreNotEqual(value, result);
        }
    }
}
