﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNet.Razor.Runtime.TagHelpers;

namespace Sandbox.Web.TagHelpers
{
    [TargetElement("p", Attributes = "data-th-style")]
    public class AppendStyleTagHelper : TagHelper
    {
        [HtmlAttributeName("data-th-style")]
        public string Style { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            TagHelperAttribute style = null;
            output.Attributes.TryGetAttribute("style", out style);

            style += Style;
            output.Attributes["style"] = style;
        }
    }
}