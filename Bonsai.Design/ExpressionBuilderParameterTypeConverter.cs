﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using Bonsai.Expressions;

namespace Bonsai.Design
{
    public class ExpressionBuilderParameterTypeConverter : TypeConverter
    {
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(Pen)) return true;

            return base.CanConvertTo(context, destinationType);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (value != null)
            {
                if (destinationType == typeof(string))
                {
                    var parameter = (ExpressionBuilderParameter)value;
                    return parameter.Value;
                }

                if (destinationType == typeof(Pen))
                {
                    var parameter = (ExpressionBuilderParameter)value;
                    if (parameter.Value == ExpressionBuilderParameter.Source) return Pens.Black;
                    if (parameter.Value == ExpressionBuilderParameter.Other) return Pens.Red;
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
