using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

namespace its2wics
{
  public static class MyExtensions
  {
    public static string NormalizeValue(this string s)
    {
      StringBuilder sb = new StringBuilder(s.Length);
      bool pending_space = false;
      foreach (char c in s)
      {
        if (" \n\r\t".IndexOf(c) >= 0)
          pending_space = true;
        else
        {
          if (pending_space)
          {
            if (sb.Length > 0) sb.Append(' ');
            pending_space = false;
          }
          sb.Append(c);
        }
      }
      return sb.ToString();
    }

    public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
    {
      var seenKeys = new HashSet<TKey>();
      return source.Where(element => seenKeys.Add(keySelector(element)));
    }
  }

  class Program
  {
    class Its2Wics
    {
      public static bool html_mode = false, with_tags = true;
      static string its = "http://www.w3.org/2005/11/its", xlink = "http://www.w3.org/1999/xlink",
                    xml = "http://www.w3.org/XML/1998/namespace", xhtml = "http://www.w3.org/1999/xhtml";
      static Uri base_uri;

      class Variables : Dictionary<string, string>
      {
        const string name = @"[\p{L}_][\w_.-]*";
        static Regex param_regex = new Regex(@"\$" + name + "(?::" + name + ")?", RegexOptions.Compiled);

        string replace_param(Match m)
        {
          string value;
          return TryGetValue(m.Value.Substring(1), out value)? value : string.Empty;
        }

        public string substitute(string s)
        {
          return s == null? null : param_regex.Replace(s, new MatchEvaluator(this.replace_param));
        }
      }

      class Rule
      {
        public Rule(XPathNavigator n, Variables v) { node = n; vars = v; }

        public XPathNavigator node;
        public Variables vars;

        public string selector
        {
          get { return vars.substitute(node.GetAttribute("selector", string.Empty)); }
        }
      }

      class Rules : List<Rule>
      {
        public Rules() {}
        public Rules(Uri base_uri, XPathNavigator root, bool html_mode) { if (html_mode) read_from_html(base_uri, root); else  read_from(base_uri, root); }

        void read_from(Uri base_uri, XPathNavigator root)
        {
          foreach (XPathNavigator rules in root.SelectDescendants("rules", its, true))
          {
            string href = rules.GetAttribute("href", xlink);
            if (!string.IsNullOrEmpty(href))
            {
              // Read external rules recursively
              string type = rules.GetAttribute("type", xlink);
              if (!string.IsNullOrEmpty(type) && type != "simple") throw new Exception("Currently unsupported link type: " + type);
              Uri uri = new Uri(base_uri, href);
              read_from(uri, new XPathDocument(uri.ToString()).CreateNavigator());
            }

            // Collect variables used in selectors
            Variables vars = new Variables();
            foreach (XPathNavigator param in rules.SelectChildren("param", its))
            {
              string name = param.GetAttribute("name", string.Empty);
              if (name != null) vars.Add(name, '\'' + param.Value + '\'');
            }

            // Collect rules (identified by the required 'selector' attribute)
            foreach (XPathNavigator rule in from XPathNavigator rule in rules.SelectChildren(XPathNodeType.Element)
                                            where !string.IsNullOrEmpty(rule.GetAttribute("selector", string.Empty)) select rule)
              Insert(0, new Rule(rule, vars));
          }
        }

        void read_from_html(Uri base_uri, XPathNavigator root)
        {
          XmlNamespaceManager nsmgr = new XmlNamespaceManager(new NameTable());
          nsmgr.AddNamespace("h", xhtml);

          foreach (XPathNavigator link in root.Select("//h:link[@rel='its-rules']", nsmgr))
          {
            string href = link.GetAttribute("href", string.Empty);
            if (!string.IsNullOrEmpty(href))
            {
              Uri uri = new Uri(base_uri, href);
              read_from(uri, new XPathDocument(uri.ToString()).CreateNavigator());
            }
          }

          foreach (XPathNavigator rules in root.Select("//h:script[@type='application/its+xml']", nsmgr))
            using (StringReader sr = new StringReader(rules.Value))
              read_from(base_uri, new XPathDocument(sr).CreateNavigator());
        }
      }

      class Values : Dictionary<string, string>
      {
        public Values() {}
        public Values(Values values) : base(values) {}
        public string data()
        {
          StringBuilder sb = new StringBuilder();
          foreach (KeyValuePair<string, string> pair in this)
          {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(pair.Key);
            sb.Append(':');
            if (pair.Key == "locQualityIssuesRef" || pair.Key == "provenanceRecordsRef")
              sb.Append(pair.Value);
            else
            {
              sb.Append('\"');
              sb.Append(pair.Value.Replace("\'", "\\'").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n"));
              sb.Append('\"');
            }
          }
          return sb.ToString();
        }
      }

      class LevelValues
      {
        public Values values;
        public int level;

        public LevelValues(Values v, int l) { values = v; level = l; }
        public LevelValues(LevelValues lv) { values = lv.values; level = lv.level; }
      }

      class WicsStyle
      {
        public string css_class, data;
        public int level;

        public WicsStyle(string c, string d, int l) { css_class = c; data = d; level = l; }
      }

      class WicsFrame
      {
        public string image1, image2, data;
        public int level;

        public WicsFrame(string i1, string i2, string d, int l) { image1 = i1; image2 = i2; data = d; level = l;  }
      }

      class Context : Dictionary<Category, LevelValues>
      {
        public XPathNavigator node, ref_node;
        public string attribute_namespace;
        public Rules rules;
        public bool parse_local_attributes;
        public Variables vars;
        public Values values, inherited_values;
        public List<WicsStyle> wics_styles;
        public List<WicsFrame> wics_frames;
        public int level;

        public Context() { level = 0; }
        public Context(XPathNavigator n) { node = ref_node = n; level = 0;  }
        public Context(XPathNavigator n, Context ctx)
        { 
          node = ref_node = n;
          level = ctx.level + 1;
          foreach (KeyValuePair<Category, LevelValues> pair in ctx) Add(pair.Key, new LevelValues(pair.Value));
        }
      }

      abstract class Expr
      {
        public abstract bool evaluate(Context ctx);
      }

      class If : Expr
      {
        Expr condition, then;
        public If(Expr c, Expr t) { condition = c; then = t; }
        public override bool evaluate(Context ctx)
        {
          bool result = condition.evaluate(ctx);
          if (result) then.evaluate(ctx);
          return result;
        }
      }

      class OneOf : Expr
      {
        Expr[] list;
        public OneOf(params Expr[] l) { list = l; }
        public override bool evaluate(Context ctx)
        {
          foreach (Expr expr in list) if (expr.evaluate(ctx)) return true;
          return false;
        }
      }

      class Any : Expr
      {
        Expr[] list;
        public Any(params Expr[] l) { list = l; }
        public override bool evaluate(Context ctx)
        {
          bool result = false;
          foreach (Expr expr in list) result |= expr.evaluate(ctx);
          return result;
        }
      }

      abstract class Data : Expr
      {
        public string name { get; set; }
        public string default_value { get; set; }
        public string default_value_for_attributes { get; set; }

        public Data() { default_value = default_value_for_attributes = null; }
        public Data(string n) : base() { name = n; }

        public virtual bool process_value(string value, Context ctx)
        {
          if (value != null) { ctx.values[name] = value.NormalizeValue(); return true; }

          value = (ctx.ref_node.NodeType == XPathNodeType.Attribute && default_value_for_attributes != null)? default_value_for_attributes : default_value;
          if (!string.IsNullOrEmpty(value)) ctx.values[name] = value.NormalizeValue();
          return false;
        }

        public virtual bool process_selected_nodes(XPathNodeIterator nodes, Context ctx)
        {
          foreach (XPathNavigator node in nodes)
          {
            if (node.NodeType == XPathNodeType.Attribute || node.NodeType == XPathNodeType.Element) return process_value(node.Value, ctx);
            throw new Exception();
          }
          return false;
        }
      }

      class Attribute : Data
      {
        public string html_name { get; set; }

        public Attribute(string n) : base(n)
        {
          StringBuilder sb = new StringBuilder("its-");
          foreach (char c in n) if (char.IsLower(c)) sb.Append(c); else { sb.Append('-'); sb.Append(char.ToLower(c)); }
          html_name = sb.ToString();
        }

        public override bool evaluate(Context ctx)
        {
          string value;
          if (!ctx.parse_local_attributes || !ctx.node.MoveToAttribute((html_mode && ctx.node.NamespaceURI == xhtml)? html_name : name, ctx.attribute_namespace))
            value = null;
          else
          {
            value = ctx.node.Value;
            ctx.node.MoveToParent();
          }
          return process_value(value, ctx);
        }
      }

      class AttributeLowerCase : Attribute
      {
        public AttributeLowerCase(string n) : base(n) {}

        public override bool process_value(string value, Context ctx)
        {
          return base.process_value((html_mode && !string.IsNullOrEmpty(value))? value.ToLowerInvariant() : value, ctx);
        }
      }

      class Element : Data
      {
        public Element(string n) : base(n) { }

        public override bool evaluate(Context ctx)
        {
          string value = null;
          foreach (XPathNavigator node in ctx.node.SelectChildren(name, its)) { value = node.Value; break; }
          return process_value(value, ctx);
        }
      }

      class Elements : Element
      {
        char separator;
        public Elements(string n, char s) : base(n) { separator = s; }

        public override bool evaluate(Context ctx)
        {
          return process_selected_nodes(ctx.node.SelectChildren(name, its), ctx);
        }

        public override bool process_selected_nodes(XPathNodeIterator nodes, Context ctx)
        {
          if (nodes.Count == 0) return process_value(null, ctx);

          StringBuilder sb = new StringBuilder();
          foreach (XPathNavigator node in nodes)
          {
            if (string.IsNullOrEmpty(node.Value)) continue;
            if (sb.Length > 0) sb.Append(separator);
            sb.Append(node.Value);
          }
          return process_value(sb.ToString(), ctx);
        }
      }

      class Pointer : Attribute
      {
        Data to;
        public Pointer(string n, Data t) : base(n) { to = t; }
        public Pointer(Data t) : this(t.name + "Pointer", t) {}

        public override bool evaluate(Context ctx)
        {
          string value = ctx.node.GetAttribute(name, ctx.attribute_namespace);
          if (string.IsNullOrEmpty(value)) return false;

          XmlNamespaceManager nsmgr = new XmlNamespaceManager(ctx.node.NameTable);
          foreach (KeyValuePair<string, string> ns in ctx.node.GetNamespacesInScope(XmlNamespaceScope.All)) nsmgr.AddNamespace(ns.Key, ns.Value);

          try
          {
            return to.process_selected_nodes(ctx.ref_node.Select(ctx.vars.substitute(value), nsmgr), ctx);
          }
          catch
          {
            throw new Exception("Invalid pointer: " + value);
          }
        }
      }

      class PseudoPointer : Attribute
      {
        public PseudoPointer(string n) : base(n) { }

        public override bool process_value(string value, Context ctx)
        {
          return base.process_value(value == null ? null : ctx.vars.substitute(value), ctx);
        }
      }

      class Standoff : Attribute
      {
        string parent_name, child_name;
        Expr expr;
        public Standoff(string n, string parent, string child, Expr e) : base(n) { parent_name = parent; child_name = child; expr = e; }

        public override bool process_value(string value, Context ctx)
        {
          if (value == null || string.IsNullOrEmpty(value = value.NormalizeValue())) return false;
          XPathNavigator reference = null;
          int i = value.LastIndexOf('#');
          bool html;
          if (i >= 0)
          {
            string iri = value.Substring(i + 1);
            if (i == 0)  // Local node?
            {
              reference = ctx.ref_node.Clone();
              reference.MoveToRoot();
              html = html_mode;
            }
            else
            {
              string path = value.Substring(0, i);
              html = Path.GetExtension(path).Equals(".html", StringComparison.InvariantCultureIgnoreCase);
              try
              {
                reference = open_document(new Uri(base_uri, path).ToString(), html).CreateNavigator();
                reference.MoveToFirstChild();
              }
              catch
              {
              }
            }

            if (reference != null)
            {
              if (html)
              {
                foreach (XPathNavigator script in from XPathNavigator node in reference.SelectDescendants("script", xhtml, true)
                                                  where node.GetAttribute("type", string.Empty) == "application/its+xml" && node.GetAttribute("id", string.Empty) == iri
                                                  select node)
                {
                  reference = new XPathDocument(new StringReader(script.Value)).CreateNavigator();
                  break;
                }
              }

              int position = 0;
              foreach (XPathNavigator parent in from XPathNavigator node in reference.SelectDescendants(parent_name, its, true)
                                                where node.GetAttribute("id", xml) == iri
                                                select node)
              {
                Context ctx2 = new Context() { ref_node = ctx.ref_node, attribute_namespace = string.Empty, parse_local_attributes = true, vars = ctx.vars, values = new Values() };
                StringBuilder sb = new StringBuilder("[");
                foreach (XPathNavigator child in parent.SelectChildren(child_name, its))
                {
                  ++position;
                  ctx2.node = child;
                  ctx2.values.Clear();
                  if (expr.evaluate(ctx2))
                  {
                    if (sb.Length > 1) sb.Append(',');
                    sb.Append('{');
                    sb.Append(ctx2.values.data());
                    sb.Append('}');
                  }
                }
                sb.Append(']');
                value = sb.ToString();
                break;
              }
            }
          }

          ctx.values[name] = value;
          return true;
        }
      };

      class GlobalRule : Expr
      {
        string name;
        Expr expr;

        public GlobalRule(string n, Expr e) { name = n; expr = e; }

        public override bool evaluate(Context ctx)
        {
          if (ctx.rules != null)
            foreach (Rule rule in ctx.rules.Where(r => r.node != null && r.node.NamespaceURI == its && r.node.LocalName == name))
            {
              bool parse_local_attributes = ctx.parse_local_attributes;
              XPathNavigator node = ctx.node;
              Variables vars = ctx.vars;
              string attribute_namespace = ctx.attribute_namespace;
              ctx.parse_local_attributes = true;
              ctx.node = rule.node;
              ctx.vars = rule.vars;
              ctx.attribute_namespace = string.Empty;
              bool result = expr.evaluate(ctx);
              ctx.parse_local_attributes = parse_local_attributes;
              ctx.node = node;
              ctx.vars = vars;
              ctx.attribute_namespace = attribute_namespace;
              return result;
            }
          return false;
        }
      }

      class Category
      {
        public Expr global { get; set; }
        public Expr local { get; set; }
        public bool inherit_elements { get; set; }
        public bool inherit_attributes { get; set; }

        public void process(Context ctx)
        {
          ctx.attribute_namespace = (ctx.node.NamespaceURI == its || html_mode && ctx.node.NamespaceURI == xhtml)? string.Empty : its;
          ctx.values = new Values();
          LevelValues inherited;
          ctx.inherited_values = ctx.TryGetValue(this, out inherited)? inherited.values : null;

          int level = -1;
          if (local != null && local.evaluate(ctx) || global != null && global.evaluate(ctx))
          {
            ctx[this] = new LevelValues(ctx.values, level = ctx.level);
          }
          else if (ctx.ref_node.NodeType == XPathNodeType.Attribute && inherit_attributes ||
                   ctx.ref_node.NodeType == XPathNodeType.Element && inherit_elements)
          {
            if (ctx.inherited_values != null) { ctx.values = ctx.inherited_values; level = inherited.level; }
          }

          make_its_styles_and_frames(ctx, level);
        }

        protected virtual void make_its_styles_and_frames(Context ctx, int level) { }

        public Category() { inherit_elements = inherit_attributes = false; }
      }


      static List<Category> categories = new List<Category>();

      //
      // Annotation
      //
      class AnnotatorsRef : Attribute
      {
        static char[] space = new char[] { ' ' }, bar = new char[] { '|' };

        public AnnotatorsRef() : base("annotatorsRef") { }

        public override bool process_value(string value, Context ctx)
        {
          if (value == null) return false;
          string inherited;
          if (ctx.inherited_values != null && ctx.inherited_values.TryGetValue(name, out inherited))
            value = string.Join(" ", (value + ' ' + inherited).Split(space).DistinctBy(s => s.Split(bar)[0]).OrderBy(s => s.Split(bar)[0]).ToArray());
          ctx.values[name] = value.NormalizeValue();
          return true;
        }
      }

      class Annotation : Category
      {
        public Annotation() { local = new AnnotatorsRef(); inherit_elements = true; inherit_attributes = true; }
      }

      //
      // Translate data category
      //
      class Translate : Category
      {
        public Translate()
        {
          Data translate = new AttributeLowerCase("translate") { html_name = "translate", default_value = "yes", default_value_for_attributes = "no" };
          global = new GlobalRule("translateRule", translate); local = translate; inherit_elements = true;
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          string value;
          if (ctx.values.TryGetValue("translate", out value))
            switch (value)
            {
              case "no":
                ctx.wics_styles.Add(new WicsStyle("wics-notranslate", ctx.values.data(), level));
                break;

              case "yes":
                if (level == ctx.level) ctx.wics_frames.Add(new WicsFrame(null, null, null, level));
                break;
            }
        }
      }

      //
      // Localization Note data category
      //
      class LocalizationNote : Category
      {
        public LocalizationNote()
        {
          Data locNote = new Element("locNote"), locNoteRef = new Attribute("locNoteRef");

          global = new GlobalRule("locNoteRule", new Any(new AttributeLowerCase("locNoteType"), new OneOf(locNote, new Pointer(locNote), locNoteRef, new Pointer(locNoteRef))));
          local = new If(new OneOf(new Attribute("locNote"), locNoteRef), new AttributeLowerCase("locNoteType") { default_value = "description" });
          inherit_elements = true;
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          if (ctx.values.ContainsKey("locNoteType"))
            ctx.wics_styles.Add(new WicsStyle("wics-locnote", level == ctx.level? ctx.values.data() : null, level));
        }
      }

      //
      // Terminology data category
      //
      class Terminology : Category
      {
        public Terminology()
        {
          Data term = new AttributeLowerCase("term") { default_value = "no" }, termInfo = new Element("termInfo"),
               termConfidence = new Attribute("termConfidence"), termInfoRef = new Attribute("termInfoRef");

          global = new GlobalRule("termRule", new Any(term, new OneOf(new Pointer(termInfo), termInfoRef, new Pointer(termInfoRef))));
          local = new If(term, new Any(termInfoRef, termConfidence));
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          string value;
          if (ctx.values.TryGetValue("term", out value))
            switch (value)
            {
              case "yes":
                ctx.wics_styles.Add(new WicsStyle("wics-terminology", ctx.values.data(), level));
                break;

              case "no":
                if (level == ctx.level) ctx.wics_frames.Add(new WicsFrame(null, null, null, level));
                break;
            }

        }
      }

      //
      // Directionality data category
      //
      class Directionality : Category
      {
        public Directionality()
        {
          Data dir = new AttributeLowerCase("dir") { html_name = "dir", default_value = "ltr" };
          global = new GlobalRule("dirRule", dir); local = dir; inherit_elements = true; inherit_attributes = true;
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          if (level == ctx.level)
          {
            string value;
            if (ctx.values.TryGetValue("dir", out value))
              switch (value)
              {
                case "ltr":
                  ctx.wics_frames.Add(new WicsFrame("ltr1", "ltr2", ctx.values.data(), level));
                  break;
                case "lro":
                  ctx.wics_frames.Add(new WicsFrame("lro1", "lro2", ctx.values.data(), level));
                  break;
                case "rtl":
                  ctx.wics_frames.Add(new WicsFrame("rtl1", "rtl2", ctx.values.data(), level));
                  break;
                case "rlo":
                  ctx.wics_frames.Add(new WicsFrame("rlo1", "rlo2", ctx.values.data(), level));
                  break;
              }
          }
        }
      }

      //
      // Language Information data category
      //
      class LanguageInformation : Category
      {
        public LanguageInformation()
        {
          global = new GlobalRule("langRule", new Pointer(new Attribute("lang"))); inherit_elements = true; inherit_attributes = true;
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          if (level == ctx.level && ctx.values.ContainsKey("lang"))
            ctx.wics_styles.Add(new WicsStyle("wics-langinfo", ctx.values.data(), level));
        }
      }

      //
      // Elements Within Text data category
      //
      class ElementsWithinText : Category
      {
        public ElementsWithinText()
        {
          Data withinText = new AttributeLowerCase("withinText") { default_value = "no", default_value_for_attributes = "" };
          global = new GlobalRule("withinTextRule", withinText); local = withinText;
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          string value;
          if (ctx.values.TryGetValue("withinText", out value))
          {
            if (value == "nested") ctx.wics_styles.Add(new WicsStyle("wics-withintext", null, level));
            if (level == ctx.level)
              switch (value)
              {
                case "yes":
                  ctx.wics_frames.Add(new WicsFrame("withinyes1", "withinyes2", ctx.values.data(), level));
                  break;
                case "nested":
                  ctx.wics_frames.Add(new WicsFrame("withinnested1", "withinnested2", ctx.values.data(), level));
                  break;
                case "no":
                  ctx.wics_frames.Add(new WicsFrame("withinno1", "withinno2", ctx.values.data(), level));
                  break;
              }
          }
        }
      }

      //
      // Domain data category
      //
      class DomainRule : GlobalRule
      {
        public DomainRule() : base("domainRule", new Any(new Pointer(new Elements("domain", ',')), new Attribute("domainMapping"))) {}

        const string domain = "(?:(?:[\"']([^,\"']+)[\"'])|([^,\"'\\s][^,\\s]*))";
        static Regex domain_regex = new Regex("\\s*[\"']?([^,]+)[\"']?\\s*", RegexOptions.Compiled);
        static Regex mapping_regex = new Regex(domain + @"\s+" + domain, RegexOptions.Compiled);

        public override bool evaluate(Context ctx)
        {
          string domain;
          if (!base.evaluate(ctx) || !ctx.values.TryGetValue("domain", out domain)) return false;

          // Replace values from the content with the values from domainMappings (if found), filter the result
          Dictionary<string, string> mapping = new Dictionary<string, string>(/*StringComparer.CurrentCultureIgnoreCase*/);
          string domainMapping;
          if (ctx.values.TryGetValue("domainMapping", out domainMapping))
            foreach (Match m in mapping_regex.Matches(domainMapping))
              mapping[m.Groups[m.Groups[1].Success? 1 : 2].Value] = m.Groups[m.Groups[3].Success? 3 : 4].Value;

          HashSet<string> domains = new HashSet<string>();
          foreach (Match m in domain_regex.Matches(domain))
          {
            string s1 = m.Groups[1].Value, s2;
            domains.Add(mapping.TryGetValue(s1, out s2)? s2 : s1);
          }

          ctx.values.Clear();
          ctx.values["domains"] = string.Join(", ", domains.ToArray());
          return true;
        }
      }

      class Domain : Category
      {
        public Domain()
        {
          global = new DomainRule(); inherit_elements = true; inherit_attributes = true;
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          if (level == ctx.level && ctx.values.ContainsKey("domains"))
            ctx.wics_frames.Add(new WicsFrame("domain", null, ctx.values.data(), level));
        }
      }

      //
      // Text Analysis data category
      //
      class TextAnalysis : Category
      {
        public TextAnalysis()
        {
          Data taConfidence = new Attribute("taConfidence"),
               taClassRef = new Attribute("taClassRef"),
               taSource = new Attribute("taSource"),
               taIdent = new Attribute("taIdent"),
               taIdentRef = new Attribute("taIdentRef");

          global = new GlobalRule("textAnalysisRule",
                     new Any(new Pointer(taClassRef),
                             new OneOf(new Any(new OneOf(taSource, new Pointer(taSource)),
                                               new OneOf(taIdent, new Pointer(taIdent))),
                                       new OneOf(taIdentRef, new Pointer(taIdentRef)))));
          local = new Any(taConfidence,
                          new Any(taClassRef,
                                  new OneOf(new Any(taSource, taIdent),
                                            taIdentRef)));
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          if (ctx.values.ContainsKey("taClassRef") || ctx.values.ContainsKey("taSource") ||
              ctx.values.ContainsKey("taIdentRef") || ctx.values.ContainsKey("taIdent"))
            ctx.wics_styles.Add(new WicsStyle("wics-textanalysis", level == ctx.level? ctx.values.data() : null, level));
        }
      }

      //
      // Locale Filter data category
      //
      class LocaleFilter : Category
      {
        public LocaleFilter()
        {
          Data localeFilterList = new Attribute("localeFilterList") { default_value = "*" },
               localeFilterType = new AttributeLowerCase("localeFilterType") { default_value = "include" };

          global = new GlobalRule("localeFilterRule", new Any(localeFilterList, localeFilterType));
          local = new Any(localeFilterList, localeFilterType);
          inherit_elements = true;
          inherit_attributes = true;
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          if (level == ctx.level && (ctx.values.ContainsKey("localeFilterList") || ctx.values.ContainsKey("localeFilterType")))
            ctx.wics_frames.Add(new WicsFrame("locfilter1", "locfilter2", ctx.values.data(), level));
        }
      }

      //
      // Provenance data category
      //
      class Provenance : Category
      {
        public Provenance()
        {
          Expr provenance_record = new Any(new OneOf(new Attribute("person"), new Attribute("personRef")),
                                           new OneOf(new Attribute("org"), new Attribute("orgRef")),
                                           new OneOf(new Attribute("tool"), new Attribute("toolRef")),
                                           new OneOf(new Attribute("revPerson"), new Attribute("revPersonRef")),
                                           new OneOf(new Attribute("revOrg"), new Attribute("revOrgRef")),
                                           new OneOf(new Attribute("revTool"), new Attribute("revToolRef")),
                                           new Attribute("provRef"));
          Data provenanceRecordsRef = new Standoff("provenanceRecordsRef", "provenanceRecords", "provenanceRecord", provenance_record);

          global = new GlobalRule("provRule", new OneOf(provenanceRecordsRef, new Pointer(provenanceRecordsRef)));
          local = new OneOf(provenance_record, provenanceRecordsRef);
          inherit_elements = true;
          inherit_attributes = true;
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          if (level == ctx.level && (ctx.values.ContainsKey("provenanceRecordsRef") ||
              ctx.values.ContainsKey("person") || ctx.values.ContainsKey("personRef") ||
              ctx.values.ContainsKey("org") || ctx.values.ContainsKey("orgRef") ||
              ctx.values.ContainsKey("tool") || ctx.values.ContainsKey("toolRef") ||
              ctx.values.ContainsKey("revPerson") || ctx.values.ContainsKey("revPersonRef") ||
              ctx.values.ContainsKey("revOrg") || ctx.values.ContainsKey("revOrgRef") ||
              ctx.values.ContainsKey("revTool") || ctx.values.ContainsKey("revToolRef") ||
              ctx.values.ContainsKey("provRef")))
          {
            ctx.wics_frames.Add(new WicsFrame("provenance1", "provenance2", ctx.values.data(), level));
          }
        }
      }

      //
      // External Resource data category
      //
      class ExternalResource : Category
      {
        public ExternalResource()
        {
          global = new GlobalRule("externalResourceRefRule", new Pointer(new Attribute("externalResourceRef")));
        }
      }

      //
      // Target Pointer data category
      //
      class TargetPointerRule : GlobalRule
      {
        public TargetPointerRule() : base("targetPointerRule", new PseudoPointer("targetPointer")) { }

        public override bool evaluate(Context ctx)
        {
          bool result = base.evaluate(ctx);
          if (ctx.rules != null)
            foreach (Rule rule in ctx.rules.Where(r => r.node == null))
            {
              ctx.values["target"] = "yes";
              result = true;
              break;
            }
          return result;
        }
      }

      class TargetPointer : Category
      {
        public TargetPointer()
        {
          global = new TargetPointerRule();
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          if (level == ctx.level)
          {
            if (ctx.values.ContainsKey("targetPointer")) ctx.wics_frames.Add(new WicsFrame("source", null, ctx.values.data(), level));
            if (ctx.values.ContainsKey("target")) ctx.wics_frames.Add(new WicsFrame("target", null, ctx.values.data(), level));
          }
        }
      }

      //
      // Id Value data category
      //
      class IdValueAttribute : Attribute
      {
        public IdValueAttribute() : base("idValue") { }

        public override bool process_value(string value, Context ctx)
        {
          string xml_id = ctx.ref_node.GetAttribute("id", xml);
          return base.process_value(string.IsNullOrEmpty(xml_id)? value : xml_id, ctx);
        }

        public override bool evaluate(Context ctx)
        {
          return process_value(null, ctx);
        }
      }

      class IdValue : Category
      {
        public IdValue()
        {
          Data idValue = new IdValueAttribute();
          global = new GlobalRule("idValueRule", new Pointer("idValue", idValue)); local = idValue;
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          if (level == ctx.level && ctx.values.ContainsKey("idValue"))
            ctx.wics_frames.Add(new WicsFrame("idvalue1", "idvalue2", ctx.values.data(), level));
        }
      }

      //
      // Preserve Space data category
      //
      class XmlSpaceAttribute : Attribute
      {
        public XmlSpaceAttribute() : base("space") { default_value = "default"; }

        public override bool evaluate(Context ctx)
        {
          string value = ctx.parse_local_attributes? ctx.node.GetAttribute(name, xml) : null;
          return process_value(value == string.Empty ? null : value, ctx);
        }
      }

      class PreserveSpace : Category
      {
        public PreserveSpace()
        {
          global = new GlobalRule("preserveSpaceRule", new Attribute("space") { default_value = "default" });
          local = new XmlSpaceAttribute();
          inherit_elements = true; inherit_attributes = true;
        }
      }

      //
      // Localization Quality Issue data category
      //

      class LocalizationQualityIssue : Category
      {
        public LocalizationQualityIssue()
        {
          Expr issue = new If(new Any(new AttributeLowerCase("locQualityIssueType"),
                                      new Attribute("locQualityIssueComment")),
                         new Any(new Attribute("locQualityIssueSeverity"),
                                 new Attribute("locQualityIssueProfileRef"),
                                 new Attribute("locQualityIssueEnabled") { default_value = "yes" }));
          Data locQualityIssuesRef = new Standoff("locQualityIssuesRef", "locQualityIssues", "locQualityIssue", issue);

          global = new GlobalRule("locQualityIssueRule", new OneOf(locQualityIssuesRef, new Pointer(locQualityIssuesRef), issue));
          local = new OneOf(issue, locQualityIssuesRef);
          inherit_elements = true;
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          if (ctx.values.ContainsKey("locQualityIssueType") ||
              ctx.values.ContainsKey("locQualityIssueComment") || ctx.values.ContainsKey("locQualityIssuesRef"))
          {
            string value;
            ctx.wics_styles.Add(new WicsStyle(ctx.values.TryGetValue("locQualityIssueEnabled", out value) &&
                                              value == "no" ? "wics-locqualityissue-disabled" : "wics-locqualityissue",
                                              level == ctx.level? ctx.values.data() : null, level));
          }
        }
      }

      //
      // Localization Quality Rating data category
      //

      class LocalizationQualityRating : Category
      {
        public LocalizationQualityRating()
        {
          local = new Any(new OneOf(new If(new Attribute("locQualityRatingScore"), new Attribute("locQualityRatingScoreThreshold")),
                                    new If(new Attribute("locQualityRatingVote"), new Attribute("locQualityRatingVoteThreshold"))),
                          new Attribute("locQualityRatingProfileRef"));
          inherit_elements = true;
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          if (level == ctx.level && (ctx.values.ContainsKey("locQualityRatingScore") || ctx.values.ContainsKey("locQualityRatingVote")))
            ctx.wics_frames.Add(new WicsFrame("locqualityrating1", "locqualityrating2", ctx.values.data(), level));
        }
      }

      //
      // MT Confidence data category
      //

      class MTConfidence : Category
      {
        public MTConfidence()
        {
          Data mtConfidence = new Attribute("mtConfidence");
          global = new GlobalRule("mtConfidenceRule", mtConfidence); local = mtConfidence; inherit_elements = true;
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          if (level == ctx.level && ctx.values.ContainsKey("mtConfidence"))
            ctx.wics_frames.Add(new WicsFrame("mtconfidence1", "mtconfidence2", ctx.values.data(), level));
        }
      }

      //
      // Allowed Characters data category
      //

      class AllowedCharacters : Category
      {
        public AllowedCharacters()
        {
          Data allowedCharacters = new Attribute("allowedCharacters");

          global = new GlobalRule("allowedCharactersRule", new OneOf(allowedCharacters, new Pointer(allowedCharacters)));
          local = allowedCharacters;
          inherit_elements = true;
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          if (level == ctx.level && ctx.values.ContainsKey("allowedCharacters"))
            ctx.wics_frames.Add(new WicsFrame("allowedchars1", "allowedchars2", ctx.values.data(), level));
        }
      }

      //
      // Storage Size data category
      //

      class StorageSize : Category
      {
        public StorageSize()
        {
          Data storageSize = new Attribute("storageSize"),
               storageEncoding = new Attribute("storageEncoding"),
               lineBreakType = new AttributeLowerCase("lineBreakType") { default_value = "lf" };

          global = new GlobalRule("storageSizeRule", new Any(new OneOf(storageSize, new Pointer(storageSize)),
                                                             new OneOf(storageEncoding, new Pointer(storageEncoding)),
                                                             lineBreakType));
          local = new If(storageSize, new Any(storageEncoding, lineBreakType));
        }

        protected override void make_its_styles_and_frames(Context ctx, int level)
        {
          if (level == ctx.level && ctx.values.ContainsKey("storageSize"))
            ctx.wics_frames.Add(new WicsFrame("storagesize1", "storagesize2", ctx.values.data(), level));
        }
      }


      class HtmlReader : Sgml.SgmlReader
      {
        public HtmlReader() { DocType = "HTML"; }
        public override string NamespaceURI { get { return (NodeType == XmlNodeType.Element && string.IsNullOrEmpty(base.NamespaceURI))? xhtml : base.NamespaceURI; } }
        public override string Name { get { return (NodeType == XmlNodeType.Element || NodeType == XmlNodeType.Attribute)? base.Name.ToLowerInvariant() : base.Name; } }
      };

      class XPathNavigatorEqualityComparer : IEqualityComparer<XPathNavigator>
      {
        public bool Equals(XPathNavigator n1, XPathNavigator n2) { return n1.IsSamePosition(n2); }
        public int GetHashCode(XPathNavigator n) { return n.Name.GetHashCode(); }
      };
      static Dictionary<XPathNavigator, Rules> binding = new Dictionary<XPathNavigator, Rules>(new XPathNavigatorEqualityComparer());


      // Create XmlDocument from XML or HTML file
      public static XmlDocument open_document(string path, bool html)
      {
        XmlDocument doc = new XmlDocument();
        doc.PreserveWhitespace = true;
        if (html)
        {
          using (HtmlReader reader = new HtmlReader())
          using (reader.InputStream = new StreamReader(path))
          using (StringWriter sw = new StringWriter())
          using (XmlTextWriter writer = new XmlTextWriter(sw))
          {
            while (reader.Read())
              if (reader.NodeType != XmlNodeType.Whitespace)
                writer.WriteNode(reader, true);
            doc.LoadXml(sw.ToString());
          }
        }
        else doc.Load(path);
        return doc;
      }

      static void apply_rules_dump_its(bool parse_local_attributes, Context ctx)
      {
        if (!binding.TryGetValue(ctx.node, out ctx.rules)) ctx.rules = null;
        ctx.parse_local_attributes = parse_local_attributes;
        ctx.wics_styles = new List<WicsStyle>();
        ctx.wics_frames = new List<WicsFrame>();
        foreach (Category category in categories) category.process(ctx);
      }

      static void wrap_in_wics_styles(XmlNode node, string namespaceURI, Context ctx)
      {
        foreach (WicsStyle style in ctx.wics_styles.OrderBy(s => s.level))
        {
          XmlElement span = node.OwnerDocument.CreateElement("span", namespaceURI);
          span.SetAttribute("class", style.css_class);
          node.ParentNode.ReplaceChild(span, node);
          span.AppendChild(node);
        }
      }

      static int wics_id = 0;
      static StringBuilder script_text = new StringBuilder();

      static XmlElement make_wics_id(XmlNode node, string namespaceURI, string data)
      {
        XmlElement span = node.OwnerDocument.CreateElement("span", namespaceURI);
        span.SetAttribute("wics-id", (wics_id++).ToString());
        span.SetAttribute("class", "wics-tip");
        if (script_text.Length > 0) script_text.Append(',');
        script_text.Append('{');
        script_text.Append(data);
        script_text.Append('}');
        return span;
      }

      static void wrap_in_wics_frames(XmlNode node, string namespaceURI, Context ctx)
      {
        XmlElement span, img;

        if (ctx.wics_styles.Count > 0)
        {
          StringBuilder sb = new StringBuilder();
          foreach (WicsStyle style in ctx.wics_styles)
          {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(style.data);
          }
          if (sb.Length > 0)
          {
            span = make_wics_id(node, namespaceURI, sb.ToString());
            node.ParentNode.ReplaceChild(span, node);
            span.AppendChild(node);
            node = span;
          }
        }

        foreach (WicsFrame frame in ctx.wics_frames.OrderBy(f => f.level).Reverse<WicsFrame>())
        {
          if (frame.image1 == null && frame.image2 == null && string.IsNullOrEmpty(frame.data))
          {
            span = node.OwnerDocument.CreateElement("span", namespaceURI);
            span.SetAttribute("class", "wics-no-tip");
            node.ParentNode.ReplaceChild(span, node);
            span.AppendChild(node);
          }
          else
          {
            span = make_wics_id(node, namespaceURI, frame.data);
            if (frame.image1 != null)
            {
              img = span.OwnerDocument.CreateElement("img", namespaceURI);
              img.SetAttribute("src", string.Format("wics/images/{0}.png", frame.image1));
              img.SetAttribute("class", "wics-image");
              span.AppendChild(img);
            }

            if (frame.image1 == "domain")
              node.ParentNode.InsertBefore(span, node);
            else
            {
              node.ParentNode.ReplaceChild(span, node);
              span.AppendChild(node);
            }

            if (frame.image2 != null)
            {
              img = span.OwnerDocument.CreateElement("img", namespaceURI);
              img.SetAttribute("src", string.Format("wics/images/{0}.png", frame.image2));
              img.SetAttribute("class", "wics-image");
              span.AppendChild(img);
            }
          }
          node = span;
        }
      }

      static void traverse1(XmlNode node, Context ctx, XmlNode dump)
      {
        XmlDocument doc = dump.OwnerDocument;
        XmlElement span;
        XmlNode text, frame = dump;
        switch (node.NodeType)
        {
          case XmlNodeType.Element:
            {
              if ((node.NamespaceURI != its || node.LocalName == "span" || node.LocalName == "ruby") &&
                  !(html_mode && node.LocalName == "script" && node.NamespaceURI == xhtml))
              {
                apply_rules_dump_its(true, ctx = new Context(node.CreateNavigator(), ctx));
              }
              dump.AppendChild(doc.CreateTextNode("<"));
              (span = doc.CreateElement("span")).SetAttribute("class", "elemname");
              span.AppendChild(doc.CreateTextNode(node.Name));
              dump.AppendChild(span);
              if (node.Attributes != null)
                foreach (XmlNode attr in node.Attributes)
                {
                  dump.AppendChild(doc.CreateTextNode(" "));
                  dump.AppendChild(span = doc.CreateElement("span"));
                  span.AppendChild(doc.CreateTextNode(attr.Name));
                  dump.AppendChild(doc.CreateTextNode("=\""));
                  XmlElement span2;
                  dump.AppendChild(span2 = doc.CreateElement("span"));
                  span2.AppendChild(text = doc.CreateTextNode(attr.InnerXml));
                  dump.AppendChild(doc.CreateTextNode("\""));
                  string name_class, value_class;
                  if (attr.LocalName == "xmlns" || attr.Prefix == "xmlns")
                  {
                    name_class = "xmlnsname"; value_class = "xmlnsvalue";
                  }
                  else if (attr.NamespaceURI == its)
                  {
                    name_class = "itsattrname"; value_class = "itsattrvalue";
                  }
                  else
                  {
                    name_class = "attrname"; value_class = "text";
                    Context ctx_ = new Context(attr.CreateNavigator(), ctx);
                    apply_rules_dump_its(false, ctx_);
                    wrap_in_wics_frames(text, null, ctx_);
                    wrap_in_wics_styles(text, null, ctx_);
                  }
                  span.SetAttribute("class", name_class);
                  span2.SetAttribute("class", value_class);
                }
              if ((node as XmlElement).IsEmpty)
                dump.AppendChild(doc.CreateTextNode(" />"));
              else
              {
                dump.AppendChild(doc.CreateTextNode(">"));
                dump.AppendChild(span = doc.CreateElement("span"));
                wrap_in_wics_frames(span, null, ctx);
                if (span.ParentNode == dump) dump.RemoveChild(span); else frame = span;
              }
            }
            break;

          case XmlNodeType.DocumentType:
          case XmlNodeType.XmlDeclaration:
            (span = doc.CreateElement("span")).SetAttribute("class", "instruction");
            span.AppendChild(doc.CreateTextNode(node.OuterXml));
            dump.AppendChild(span);
            break;

          case XmlNodeType.Text:
            (span = doc.CreateElement("span")).SetAttribute("class", "text");
            span.AppendChild(text = doc.CreateTextNode(node.OuterXml));
            wrap_in_wics_styles(text, null, ctx);
            dump.AppendChild(span);
            break;

          case XmlNodeType.Comment:
            (span = doc.CreateElement("span")).SetAttribute("class", "comment");
            span.AppendChild(doc.CreateTextNode(node.OuterXml));
            dump.AppendChild(span);
            break;

          case XmlNodeType.CDATA:
            span = doc.CreateElement("span");
            if (html_mode)
            {
              span.SetAttribute("class", "script");
              span.AppendChild(doc.CreateTextNode(node.Value));
              dump.AppendChild(span);
            }
            else
            {
              span.SetAttribute("class", "text");
              span.AppendChild(text = doc.CreateTextNode(node.OuterXml));
              wrap_in_wics_styles(text, null, ctx);
              dump.AppendChild(span);
              dump.AppendChild(doc.CreateTextNode("]]>"));
            }
            break;

          case XmlNodeType.SignificantWhitespace:
          case XmlNodeType.Whitespace:
            dump.AppendChild(doc.CreateWhitespace(node.OuterXml));
            break;
        }

        foreach (XmlNode child in node.ChildNodes)
          traverse1(child, ctx, frame);

        switch (node.NodeType)
        {
          case XmlNodeType.Element:
            if (!(node as XmlElement).IsEmpty)
            {
              dump.AppendChild(doc.CreateTextNode("</"));
              (span = doc.CreateElement("span")).SetAttribute("class", "elemname");
              span.AppendChild(doc.CreateTextNode(node.Name));
              dump.AppendChild(span);
              dump.AppendChild(doc.CreateTextNode(">"));
            }
            break;
        }
      }

      static XmlNode body_deputy, body_deputy_inner, org_body, body = null;

      static void traverse2(XmlNode node, Context ctx, XmlNode dump, bool in_body)
      {
        XmlDocument doc = dump.OwnerDocument == null? dump as XmlDocument : dump.OwnerDocument;
        XmlNode node2 = doc.ImportNode(node, false);
        dump.AppendChild(node2);

        bool wrap = false;
        switch (node.NodeType)
        {
          case XmlNodeType.Element:
            (node2 as XmlElement).IsEmpty = (node as XmlElement).IsEmpty;
            if (html_mode && !in_body && node == org_body) { body = node2; in_body = true; }
            if ((node.NamespaceURI != its || node.LocalName == "span" || node.LocalName == "ruby") &&
                !(html_mode && node.LocalName == "script" && node.NamespaceURI == xhtml))
            {
              apply_rules_dump_its(true, ctx = new Context(node.CreateNavigator(), ctx));
              wrap = true;
            }

            if (node.Attributes != null)
              foreach (XmlAttribute attr in node.Attributes)
              {
                (node2 as XmlElement).SetAttributeNode(doc.ImportNode(attr, true) as XmlAttribute);
                if (html_mode && string.IsNullOrEmpty(attr.NamespaceURI) && attr.LocalName == "alt" && node.NamespaceURI == xhtml && node.LocalName == "img")
                {
                  Context ctx_ = new Context(attr.CreateNavigator(), ctx);
                  apply_rules_dump_its(false, ctx_);
                  if (ctx_.wics_styles.Count > 0 || ctx_.wics_frames.Count > 0)
                  {
                    ctx.wics_styles.AddRange(ctx_.wics_styles);
                    ctx.wics_frames.AddRange(ctx_.wics_frames);
                    wrap_in_wics_styles(node2, xhtml, ctx_);
                    wrap = true;
                  }
                }
              }
            break;

          case XmlNodeType.Text:
            if (!html_mode || in_body) wrap_in_wics_styles(node2, xhtml, ctx);
            break;

          case XmlNodeType.CDATA:
            if (html_mode)
            {
              XmlDocumentFragment fragment = doc.CreateDocumentFragment();
              fragment.InnerXml = node.Value;
              dump.ReplaceChild(fragment, node2);
            }
            break;
        }

        dump = node2;
        foreach (XmlNode child in node.ChildNodes)
        {
          if (html_mode && !in_body && org_body == null && node == node.OwnerDocument.DocumentElement && child.NodeType == XmlNodeType.Element && child.NamespaceURI == xhtml)
          {
            bool meta = false;
            switch (child.LocalName)
            {
              case "base":
              case "link":
              case "meta":
              case "noscript":
              case "script":
              case "title":
                meta = true;
                break;

              case "style":
                meta = child.Attributes.GetNamedItem("scoped", xhtml) == null;
                break;
            }

            if (!meta)
            {
              dump.AppendChild(body = dump.OwnerDocument.CreateElement("body", xhtml));
              dump = body;
              in_body = true;
            }
          }
          traverse2(child, ctx, dump, in_body);
        }

        if (wrap)
        {
          if (html_mode && !in_body) dump = body_deputy_inner;
          if (html_mode && node.NamespaceURI == xhtml)
            switch (node.LocalName)
            {
              case "body":
              case "dd":
              case "dt":
              case "h1":
              case "h2":
              case "h3":
              case "h4":
              case "h5":
              case "h6":
              case "li":
              case "p":
              case "th":
              case "td":
                if (dump.ChildNodes.Count == 1)
                  wrap_in_wics_frames(dump.FirstChild, xhtml, ctx);
                else
                {
                  XmlElement span = doc.CreateElement("span", xhtml);
                  while (dump.FirstChild != null) span.AppendChild(dump.FirstChild);
                  dump.AppendChild(span);
                  wrap_in_wics_frames(span, xhtml, ctx);
                  while (span.FirstChild != null) span.ParentNode.InsertBefore(span.FirstChild, span);
                  span.ParentNode.RemoveChild(span);
                }
                return;
            }
            wrap_in_wics_frames(dump, xhtml, ctx);
        }
      }

      public static void process(string in_path, string out_path)
      {
        categories.Add(new Annotation());
        categories.Add(new AllowedCharacters());
        categories.Add(new Directionality());
        categories.Add(new Domain());
        categories.Add(new ElementsWithinText());
        categories.Add(new ExternalResource());
        categories.Add(new IdValue());
        categories.Add(new LanguageInformation());
        categories.Add(new LocaleFilter());
        categories.Add(new LocalizationNote());
        categories.Add(new LocalizationQualityIssue());
        categories.Add(new LocalizationQualityRating());
        categories.Add(new MTConfidence());
        categories.Add(new Provenance());
        categories.Add(new StorageSize());
        categories.Add(new TargetPointer());
        categories.Add(new Terminology());
        categories.Add(new TextAnalysis());
        categories.Add(new Translate());
        if (!html_mode) categories.Add(new PreserveSpace());

        XmlDocument doc = open_document(in_path, html_mode);
        if (doc.DocumentElement == null) return;
        XPathNavigator root = doc.DocumentElement.CreateNavigator();

        base_uri = new Uri(Path.GetFullPath(in_path));

        // Collect ITS rules...
        foreach (Rule rule in new Rules(base_uri, root, html_mode))
        {
          XmlNamespaceManager nsmgr = new XmlNamespaceManager(rule.node.NameTable);
          foreach (KeyValuePair<string, string> ns in rule.node.GetNamespacesInScope(XmlNamespaceScope.All)) nsmgr.AddNamespace(ns.Key, ns.Value);

          string target_pointer = null;
          if (rule.node.NamespaceURI == its && rule.node.LocalName == "targetPointerRule")
          {
            target_pointer = rule.vars.substitute(rule.node.GetAttribute("targetPointer", string.Empty));
          }

          // ...and bind them to the nodes
          foreach (XPathNavigator node in from XPathNavigator node in root.Select(rule.selector, nsmgr) select node)
          {
            Rules rules;
            if (!binding.TryGetValue(node, out rules)) binding.Add(node, rules = new Rules());
            rules.Add(rule);

            if (target_pointer != null)
            {
              XPathNavigator src_node = node.SelectSingleNode(target_pointer, nsmgr);
              if (src_node != null)
              {
                if (!binding.TryGetValue(src_node, out rules)) binding.Add(src_node, rules = new Rules());
                rules.Add(new Rule(null, null));
              }
            }
          }
        }

        // Apply ITS rules and create test output
        XmlDocument doc2;
        XmlElement head, link, script;
        if (with_tags)
        {
          doc2 = new XmlDocument();
          XmlElement html = doc2.CreateElement("html"), meta = doc2.CreateElement("meta"), body = doc2.CreateElement("body");
          head = doc2.CreateElement("head");
          link = doc2.CreateElement("link");
          script = doc2.CreateElement("script");
          meta.SetAttribute("charset", "utf-8");
          head.AppendChild(meta);
          link.SetAttribute("href", "wics/styles/tags.css");
          link.SetAttribute("rel", "stylesheet");
          link.SetAttribute("type", "text/css");
          head.AppendChild(link);
          link = link.Clone() as XmlElement;
          html.AppendChild(head);
          html.AppendChild(body);
          doc2.AppendChild(html);

          traverse1(doc, new Context(root), body);
        }
        else
        {
          XmlNamespaceManager nsmgr = new XmlNamespaceManager(new NameTable());
          nsmgr.AddNamespace("h", xhtml);
          doc2 = doc.CloneNode(false) as XmlDocument;
          org_body = doc.SelectSingleNode("//h:body", nsmgr);
          body_deputy = doc2.CreateElement("body"); body_deputy_inner = doc2.CreateElement("span"); body_deputy.AppendChild(body_deputy_inner);
          Context ctx = new Context(root);
          foreach (XmlNode node in doc.ChildNodes)
            traverse2(node, ctx, doc2, false);
          if (body_deputy_inner.ParentNode != body_deputy && body != null)
          {
            while (body.FirstChild != null) body_deputy_inner.ParentNode.InsertBefore(body.FirstChild, body_deputy_inner);
            body_deputy_inner.ParentNode.RemoveChild(body_deputy_inner);
            while (body_deputy.FirstChild != null) body.AppendChild(body_deputy.FirstChild);
            body_deputy.AppendChild(body_deputy_inner);
          }
          head = doc2.SelectSingleNode("//h:head", nsmgr) as XmlElement;
          if (head == null)
          {
            head = doc2.CreateElement("head", xhtml);
            doc2.DocumentElement.PrependChild(head);
          }
          link = doc2.CreateElement("link", xhtml);
          script = doc2.CreateElement("script", xhtml);
        }

        link.SetAttribute("href", "wics/styles/main.css");
        link.SetAttribute("rel", "stylesheet");
        link.SetAttribute("type", "text/css");
        head.AppendChild(link);
        script.IsEmpty = false;
        script.SetAttribute("type", "text/javascript");
        script.SetAttribute("src", "wics/jquery-1.9.1.min.js");
        head.AppendChild(script);
        script = script.Clone() as XmlElement;
        script.SetAttribute("src", "wics/wics.js");
        head.AppendChild(script);
        script = script.Clone() as XmlElement;
        script.RemoveAttribute("src");
        script_text.Insert(0, "wics_tips = [");
        script_text.Append("];");
        script.AppendChild(script.OwnerDocument.CreateTextNode(script_text.ToString()));
        head.AppendChild(script);

        XmlWriterSettings xws = new XmlWriterSettings();
        xws.OmitXmlDeclaration = true;
        xws.Indent = false;
        using (StreamWriter tw = new StreamWriter(out_path))
        using (XmlWriter writer = XmlWriter.Create(tw, xws))
        {
          tw.WriteLine("<!DOCTYPE html>");
          doc2.Save(writer);
        }
      }
    }

    static int help()
    {
      Console.WriteLine("Usage: ITS2WICS <input XML or HTML file> <output HTML file> [-t]\n\n" +
                        "  -t   Force output with tags for HTML");
      return 1;
    }

    static int Main(string[] args)
    {
      try
      {
        string in_path = null, out_path = null;
        bool force_with_tags = false;

        for (int i = 0; i < args.Length; ++i)
        {
          string arg = args[i];
          if (arg[0] == '-' || arg[0] == '/')
          {
            if (arg.Length == 2)
              switch (arg[1])
              {
                case 't':
                  force_with_tags = true;
                  continue;
                case '?':
                  return help();
              }
            throw new Exception("Invalid switch: " + arg);
          }
          else if (in_path == null)
            in_path = arg;
          else if (out_path == null)
            out_path = arg;
          else
            throw new Exception("Unexpected extra argument: " + arg);
        }

        if (out_path == null) return help();

        string ext = Path.GetExtension(in_path);
        if (ext.Equals(".html", StringComparison.InvariantCultureIgnoreCase) || ext.Equals(".htm", StringComparison.InvariantCultureIgnoreCase))
        {
          Its2Wics.html_mode = true;
          Its2Wics.with_tags = force_with_tags;
        }

        Its2Wics.process(args[0], args[1]);
        return 0;
      }
      catch (Exception e)
      {
        Console.WriteLine(e.Message);
                        if (e.Source != null) Console.WriteLine(e.Source);
                        if (e.StackTrace != null) Console.WriteLine(e.StackTrace);
      }
      return 1;
    }
  }
}
