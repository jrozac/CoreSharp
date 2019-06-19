﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;

namespace CoreSharp.Breeze.Query {
  public class DataType {
    private string _name;
    private Type _type;
    private static Dictionary<string, DataType> _nameMap = new Dictionary<string, DataType>();
    private static Dictionary<Type, DataType> _typeMap = new Dictionary<Type, DataType>();

    public static DataType Binary = new DataType("Binary");
    public static DataType Guid = new DataType("Guid", typeof(System.Guid));
    public static DataType String = new DataType("String", typeof(string));

    public static DataType DateTime = new DataType("DateTime", typeof(DateTime));
    public static DataType DateTimeOffset = new DataType("DateTimeOffset", typeof(DateTimeOffset));
    public static DataType Time = new DataType("Time", typeof(TimeSpan));

    public static DataType Byte = new DataType("Byte", typeof(byte));
    public static DataType Int16 = new DataType("Int16", typeof(short));
    public static DataType Int32 = new DataType("Int32", typeof(int));
    public static DataType Int64 = new DataType("Int64", typeof(long));
    public static DataType Boolean = new DataType("Boolean", typeof(bool));

    public static DataType Decimal = new DataType("Decimal", typeof(decimal));
    public static DataType Double = new DataType("Double", typeof(double));
    public static DataType Single = new DataType("Single", typeof(float));



    public DataType(string name) {
      _name = name;
      _nameMap[name] = this;
    }

    public DataType(string name, Type type) {
      _name = name;
      _type = type;
      _nameMap[name] = this;
      _typeMap[type] = this;
    }


    public string GetName() {
      return _name;
    }

    public Type GetUnderlyingType() {
      return _type;
    }

    public static DataType FromName(string name) {
      return _nameMap[name];
    }

    public static DataType FromType(Type type) {
      var nnType = TypeFns.GetNonNullableType(type);
      return _typeMap[nnType];
    }

    // Can't use this safely because of missing support for optional parts.
    // private static DateFormat ISO8601_Format = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSSZ");

    public static object CoerceData(object value, DataType dataType) {

      if (value == null || dataType == null || value.GetType() == dataType.GetUnderlyingType()) {
        return value;
      } else if (value is IList) {
        // this occurs with an 'In' clause
        var itemType = dataType.GetUnderlyingType();
        var listType = typeof(List<>).MakeGenericType(new[] { itemType });
        var newList = (IList)Activator.CreateInstance(listType);
        foreach (var item in value as IList) {
          newList.Add(CoerceData(item, dataType));
        }
        return newList;

      } else if (dataType == DataType.Guid) {
        return System.Guid.Parse(value.ToString());
      } else if (dataType == DataType.DateTimeOffset && value is DateTime) {
        DateTimeOffset result = (DateTime)value;
        return result;
      } else if (dataType == DataType.Time && value is string) {
        return XmlConvert.ToTimeSpan((string)value);
      } else {
        return Convert.ChangeType(value, dataType.GetUnderlyingType());
      }


    }
  }
}
