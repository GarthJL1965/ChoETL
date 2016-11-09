﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Data;

namespace ChoETL
{
    public static class ChoConvert
    {
        public static readonly CultureInfo DefaultCulture = CultureInfo.CurrentCulture;
        private const string ImplicitOperatorMethodName = "op_Implicit";
        private const string ExplicitOperatorMethodName = "op_Explicit";

        public static bool TryConvertTo(object value, Type targetType, CultureInfo culture, out object output)
        {
            output = (object)null;
            try
            {
                output = ChoConvert.ConvertTo(value, targetType, culture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static object ConvertTo(object value, Type targetType, CultureInfo culture = null)
        {
            if (value == null)
                return ChoConvert.ConvertTo(value, targetType, value, (object[])null, (object[])null, culture);

            Type origType = targetType;
            Type type = value == null ? typeof(object) : value.GetType();
            if (type == origType)
                return value;

            return ChoConvert.ConvertTo(value, targetType, value, ChoTypeDescriptor.GetTypeConverters(value.GetType()), ChoTypeDescriptor.GetTypeConverterParams(value.GetType()), culture);
        }

        public static bool TryConvertFrom(object value, MemberInfo memberInfo, object sourceObject, CultureInfo culture, out object output)
        {
            output = (object)null;
            ChoGuard.ArgumentNotNull((object)memberInfo, "MemberInfo");
            try
            {
                output = ChoConvert.ConvertFrom(value, memberInfo, sourceObject, culture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static object ConvertFrom(object value, MemberInfo memberInfo, object sourceObject = null, CultureInfo culture = null)
        {
            ChoGuard.ArgumentNotNull((object)memberInfo, "MemberInfo");
            return ChoConvert.ConvertFrom(value, ChoType.GetMemberType(memberInfo), sourceObject, ChoTypeDescriptor.GetTypeConverters(memberInfo), ChoTypeDescriptor.GetTypeConverterParams(memberInfo), culture);
        }

        public static object ConvertFrom(object value, Type targetType, object sourceObject = null, object[] converters = null, object[] parameters = null, CultureInfo culture = null)
        {
            Type origType = targetType;
            object obj1 = value;
            if (targetType == (Type)null)
                return value;
            if (targetType == typeof(object))
                return value;
            if (culture == null)
                culture = ChoConvert.DefaultCulture;
            Type type = value == null ? typeof(object) : value.GetType();
            try
            {
                if (converters != null && converters.Length > 0)
                {
                    object[] objArray = (object[])null;
                    for (int index = 0; index < converters.Length; ++index)
                    {
                        object obj2 = converters[index];
                        if (parameters != null && parameters.Length > 0)
                            objArray = parameters[index] as object[];
                        if (obj2 is TypeConverter)
                        {
                            TypeConverter typeConverter = obj2 as TypeConverter;
                            if (typeConverter.CanConvertFrom(type))
                                value = typeConverter.ConvertFrom((ITypeDescriptorContext)null, culture, value);
                        }
                        else if (obj2 is IValueConverter)
                            value = ((IValueConverter)obj2).Convert(value, targetType, (object)objArray, culture);
                    }
                    //if (value != obj1)
                    //    return value;
                }
                if (value == null)
                    return targetType.Default();
                targetType = targetType.IsNullableType() ? targetType.GetUnderlyingType() : targetType;
                if (targetType.IsAssignableFrom(value.GetType()) || targetType == value.GetType())
                    return value;
                if (value is IConvertible)
                {
                    try
                    {
                        value = Convert.ChangeType(value, targetType, (IFormatProvider)culture);
                        if (obj1 != value)
                            return value;
                    }
                    catch
                    {
                    }
                }
                if (ChoConvert.TryConvertXPlicit(value, targetType, "op_Explicit", ref value) 
                    || ChoConvert.TryConvertXPlicit(value, targetType, "op_Implicit", ref value))
                    return value;

                object convValue = null;
                if (origType.IsNullableType())
                    return null;
                else if (ChoConvert.TryConvertToSpecialValues(value, targetType, culture, out convValue))
                    return convValue;

                throw new ApplicationException("Object conversion failed.");
            }
            catch (Exception ex)
            {
                if (type.IsSimple())
                    throw new ApplicationException(string.Format("Can't convert '{2}' value from '{0}' type to '{1}' type.", (object)type, (object)targetType, value), ex);
                throw new ApplicationException(string.Format("Can't convert object from '{0}' type to '{1}' type.", (object)type, (object)targetType), ex);
            }
        }

        private static bool TryConvertXPlicit(object value, Type destinationType, string operatorMethodName, ref object result)
        {
            return ChoConvert.TryConvertXPlicit(value, value.GetType(), destinationType, operatorMethodName, ref result) || ChoConvert.TryConvertXPlicit(value, destinationType, destinationType, operatorMethodName, ref result);
        }

        private static bool TryConvertXPlicit(object value, Type invokerType, Type destinationType, string xPlicitMethodName, ref object result)
        {
            foreach (MethodInfo methodInfo in Enumerable.Where<MethodInfo>((IEnumerable<MethodInfo>)invokerType.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public), (Func<MethodInfo, bool>)(m => m.Name == xPlicitMethodName)))
            {
                if (destinationType.IsAssignableFrom(methodInfo.ReturnType))
                {
                    ParameterInfo[] parameters = methodInfo.GetParameters();
                    if (Enumerable.Count<ParameterInfo>((IEnumerable<ParameterInfo>)parameters) == 1)
                    {
                        if (parameters[0].ParameterType == value.GetType())
                        {
                            try
                            {
                                result = methodInfo.Invoke((object)null, new object[1]
                                {
                                    value
                                });
                                return true;
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
            return false;
        }

        private static bool TryConvertToSpecialValues(object value, Type targetType, CultureInfo culture, out object result)
        {
            result = (object)null;
            if (value is string && ((string)value).Length == 0)
            {
                result = targetType.Default();
                return true;
            }
            if (targetType.IsEnum)
            {
                if (value is string)
                {
                    result = Enum.Parse(targetType, value as string);
                    if (Enum.IsDefined(targetType, result))
                        return true;
                }
                result = Enum.ToObject(targetType, value);
                return true;
            }
            if (value is string && targetType == typeof(Guid))
            {
                result = (object)new Guid(value as string);
                return true;
            }
            if (value is string && targetType == typeof(Version))
            {
                result = (object)new Version(value as string);
                return true;
            }
            if (targetType == typeof(string))
            {
                result = (object)value.ToString();
                return true;
            }
            return false;
        }

        public static bool TryConvertTo(object value, MemberInfo memberInfo, Type targetType, object sourceObject, CultureInfo culture, out object output)
        {
            output = (object)null;
            ChoGuard.ArgumentNotNull((object)memberInfo, "MemberInfo");
            try
            {
                output = ChoConvert.ConvertTo(value, targetType, sourceObject, ChoTypeDescriptor.GetTypeConverters(memberInfo), ChoTypeDescriptor.GetTypeConverterParams(memberInfo), culture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static object ConvertTo(object value, MemberInfo memberInfo, Type targetType, object sourceObject = null, CultureInfo culture = null)
        {
            ChoGuard.ArgumentNotNull((object)memberInfo, "MemberInfo");
            return ChoConvert.ConvertTo(value, targetType, sourceObject, ChoTypeDescriptor.GetTypeConverters(memberInfo), ChoTypeDescriptor.GetTypeConverterParams(memberInfo), culture);
        }

        private static object ConvertTo(object value, Type targetType, object sourceObject, object[] converters, object[] parameters, CultureInfo culture)
        {
            Type origType = targetType;
            object obj1 = value;
            if (targetType == (Type)null)
                return value;
            if (targetType == typeof(object))
                return value;
            if (culture == null)
                culture = ChoConvert.DefaultCulture;
            Type type = value == null ? typeof(object) : value.GetType();

            try
            {
                object[] objArray = (object[])null;
                if (converters != null && converters.Length > 0)
                {
                    for (int index = 0; index < converters.Length; ++index)
                    {
                        object obj2 = converters[index];
                        if (parameters != null && parameters.Length > 0)
                            objArray = parameters[index] as object[];
                        if (obj2 is TypeConverter)
                        {
                            TypeConverter typeConverter = obj2 as TypeConverter;
                            if (typeConverter.CanConvertTo(targetType))
                                value = typeConverter.ConvertTo((ITypeDescriptorContext)null, culture, value, targetType);
                        }
                        else if (obj2 is IValueConverter)
                            value = ((IValueConverter)obj2).ConvertBack(value, targetType, (object)objArray, culture);
                    }
                    if (obj1 != value)
                        return value;
                }
                if (value == null)
                    return targetType.Default();
                if (type == origType)
                    return value;
                targetType = targetType.IsNullableType() ? targetType.GetUnderlyingType() : targetType;
                if (targetType.IsAssignableFrom(value.GetType()) || targetType == value.GetType())
                    return value;
                if (value is IConvertible)
                {
                    try
                    {
                        value = Convert.ChangeType(value, targetType, (IFormatProvider)culture);
                        if (obj1 != value)
                            return value;
                    }
                    catch
                    {
                    }
                }
                if (ChoConvert.TryConvertXPlicit(value, targetType, "op_Explicit", ref value) 
                    || ChoConvert.TryConvertXPlicit(value, targetType, "op_Implicit", ref value))
                    //|| (!origType.IsNullableType() && ChoConvert.TryConvertToSpecialValues(value, targetType, culture, out value)))
                    //  || ChoConvert.TryConvertToSpecialValues(value, targetType, culture, out value))
                    return value;

                if (origType.IsNullableType())
                    return null;
                else if (ChoConvert.TryConvertToSpecialValues(value, targetType, culture, out value))
                    return value;
                
                throw new ApplicationException("Object conversion failed.");
            }
            catch (Exception ex)
            {
                if (type.IsSimple())
                    throw new ApplicationException(string.Format("Can't convert '{2}' value from '{0}' type to '{1}' type.", (object)type, (object)targetType, value), ex);
                throw new ApplicationException(string.Format("Can't convert object from '{0}' type to '{1}' type.", (object)type, (object)targetType), ex);
            }
        }

        // Summary:
        //     Returns an object of the specified type and whose value is equivalent to
        //     the specified object.
        //
        // Parameters:
        //   value:
        //     An object that implements the System.IConvertible interface.
        //
        //   conversionType:
        //     The type of object to return.
        //
        // Returns:
        //     An object whose type is conversionType and whose value is equivalent to value.-or-A
        //     null reference (Nothing in Visual Basic), if value is null and conversionType
        //     is not a value type.
        //
        // Exceptions:
        //   System.InvalidCastException:
        //     This conversion is not supported. -or-value is null and conversionType is
        //     a value type.-or-value does not implement the System.IConvertible interface.
        //
        //   System.FormatException:
        //     value is not in a format recognized by conversionType.
        //
        //   System.OverflowException:
        //     value represents a number that is out of the range of conversionType.
        //
        //   System.ArgumentNullException:
        //     conversionType is null.
        public static object ChangeType(object value, Type conversionType)
        {
            if (value == null)
                return ChoActivator.CreateInstance(conversionType);

            if (conversionType.IsAssignableFrom(value.GetType()))
                return value;

            object dest = null;
            if (value is IConvertible)
                dest = Convert.ChangeType(value, conversionType);
            else
            {
                dest = ChoActivator.CreateInstance(conversionType);
                value.CloneTo(dest);
            }

            if (dest != null && !dest.GetType().IsSimple())
            {
                ChoObjectValidationMode m = GetValidationMode(value);
                if (m == ChoObjectValidationMode.MemberLevel)
                    ChoValidator.Validate(dest);
                else if (m == ChoObjectValidationMode.ObjectLevel)
                    ChoValidator.Validate(dest);
            }
            return dest;
        }

        public static object ChangeType<T>(object value, Type conversionType)
            where T : Attribute
        {
            if (value == null)
                return ChoActivator.CreateInstance(conversionType);

            if (conversionType.IsAssignableFrom(value.GetType()))
                return value;

            object dest = null;
            if (value is IConvertible)
                dest = Convert.ChangeType(value, conversionType);
            else
            {
                dest = ChoActivator.CreateInstance(conversionType);
                value.CloneTo<T>(dest);
            }

            if (dest != null && !dest.GetType().IsSimple())
            {
                ChoObjectValidationMode m = GetValidationMode(value);
                if (m == ChoObjectValidationMode.MemberLevel)
                    ChoValidator.Validate(dest);
                else if (m == ChoObjectValidationMode.ObjectLevel)
                    ChoValidator.Validate(dest);
            }
            return dest;
        }

        private static ChoObjectValidationMode GetValidationMode(object value)
        {
            if (value == null) return ChoObjectValidationMode.Off;

            ChoObjectAttribute attr = ChoType.GetAttribute<ChoObjectAttribute>(value.GetType());
            return attr != null ? attr.ObjectValidationMode : ChoObjectValidationMode.Off;
        }
    }
}
