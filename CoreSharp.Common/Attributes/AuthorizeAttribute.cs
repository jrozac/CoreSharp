﻿using System;

namespace CoreSharp.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AuthorizeAttribute : Attribute
    {
        public string? Permission { get; set; }

        public AuthorizeAttribute()
        {

        }

        public AuthorizeAttribute(string permission)
        {
            Permission = permission;
        }
    }
}
