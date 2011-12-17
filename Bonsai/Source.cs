﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reactive.Disposables;
using System.ComponentModel;

namespace Bonsai
{
    public abstract class Source : WorkflowElement
    {
        public abstract void Start();

        public abstract void Stop();
    }

    public abstract class Source<T> : Source
    {
        OutputObservable<T> output;

        protected Source()
        {
            output = new OutputObservable<T>();
        }

        [Browsable(false)]
        public IObservable<T> Output
        {
            get { return output; }
        }

        protected virtual void OnOutput(T value)
        {
            output.OnNext(value);
        }
    }
}
