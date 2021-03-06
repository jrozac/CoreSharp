﻿using CoreSharp.Common.Attributes;
using CoreSharp.Validation.Attributes;

namespace CoreSharp.Validation.Tests.Models.Attributes
{
    [IgnoreValidationAttributes(Properties = new []{"Name"})]
    public class IgnoreValidationAttributesModel
    {
        [NotNull]
        public string Name { get; set; }
    }
}
