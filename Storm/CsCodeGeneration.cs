using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Esprima.NET;
using Esprima.NET.Syntax;

namespace Storm
{
    public class CsCodeGeneration : ICodeGeneration
    {
        private readonly bool _debugMode;
        private readonly Context _context;

        public CsCodeGeneration(bool debugMode, Context context)
        {
            _debugMode = debugMode;
            _context = context;
        }

        protected bool DeclarationContext { get; set; }
        protected bool ReturnContext { get; set; }

        private string TypeAsString(Type type)
        {
            var genericArgs = string.Join(",", type.GenericTypeArguments.ToList().Select(TypeAsString));
            return string.Format(
                "{0}{1}",
                type.FullName.Split('`')[0],
                !string.IsNullOrWhiteSpace(genericArgs)
                    ? "<" + genericArgs + ">"
                    : "");
        }

        public string Generate(ISyntax syntax)
        {
            var sb = new StringBuilder();
            var typeName = syntax.GetType().Name;
            switch (typeName)
            {
                    #region "Program"

                case "Program":
                    var program = (syntax as Program);
                    sb.Append("using System;");
                    sb.Append("using Storm;");
                    sb.Append("public class C0 : JsObject");
                    sb.Append("{");

                    _context.DeclaredVarNames.ToList().ForEach(
                        p => sb.Append(string.Format("private object {0}{{get;set;}}", p)));

                    _context.Actions.ToList().ForEach(
                        a => sb.Append(string.Format("private {0} {1};", TypeAsString(a.Value.GetType()), a.Key)));

                    sb.Append("public C0(");

                    _context.Actions.ToList().ForEach(
                        a => sb.Append(string.Format("{0} {1}, ", TypeAsString(a.Value.GetType()), a.Key)));

                    sb.Append("IDebugger debugger):base(debugger){");

                    _context.Actions.ToList().ForEach(a => sb.Append(string.Format("this.{0} = {0};", a.Key)));

                    sb.Append("}");

                    this.DeclarationContext = true;
                    // colocar esce��o para tipo n�o suportado
                    program.Body.ToList().Where(inst => inst is VariableDeclaration).ToList().ForEach(
                        b => sb.Append(b.ToString()));
                    this.DeclarationContext = false;

                    sb.Append("public override object Exec()");
                    sb.Append("{");
                    var ret = false;
                    var index = 0;
                    program.Body.ForEach(b =>
                                             {
                                                 if (index == program.Body.Count - 1)
                                                 {
                                                     if (b.Type == "ExpressionStatement")
                                                     {
                                                         if (b.Expression.Type == "CallExpression")
                                                         {
                                                             if (_context.Actions.ContainsKey(b.Expression.Callee.Name))
                                                             {
                                                                 if (
                                                                     TypeAsString(
                                                                         _context.Actions[b.Expression.Callee.Name].
                                                                             GetType()).
                                                                         StartsWith("System.Func"))
                                                                 {
                                                                     sb.Append("return ");
                                                                     ret = true;
                                                                 }
                                                             }
                                                             else throw new Exception();
                                                         }
                                                         else if (b.Type == "Literal"
                                                                  || b.Type == "Identifier"
                                                                  || b.Type == "ExpressionStatement")
                                                         {
                                                             sb.Append("return ");
                                                             ret = true;
                                                         }
                                                     }
                                                     else if (b.Type == "Literal"
                                                              || b.Type == "Identifier"
                                                              || b.Type == "ExpressionStatement")
                                                     {
                                                         sb.Append("return ");
                                                         ret = true;
                                                     }
                                                 }

                                                 sb.Append(b.ToString());
                                                 index++;
                                             });
                    this.ReturnContext = true;
                    if (!ret)
                        sb.Append("return JsObject.Undefined;");
                    this.ReturnContext = true;
                    sb.Append("}");

                    sb.Append("}");

                    break;

                    #endregion

                    #region "VariableDeclaration":

                case "VariableDeclaration":
                    var variableDeclaration = (syntax as VariableDeclaration);

                    if (this.ReturnContext)
                    {
                        sb.Append(variableDeclaration.Declarations.Last().ToString());
                    }
                    else
                    {
                        foreach (var d in variableDeclaration.Declarations)
                        {
                            if (_debugMode && !this.DeclarationContext)
                                sb.Append(string.Format("Debugger.BreakPoint({0}, {1}, {2}, {3}, {4}, {5});",
                                                        d.Range.Start, d.Range.End, d.Loc.Start.Line, d.Loc.Start.Column,
                                                        d.Loc.End.Line, d.Loc.End.Column));

                            if (this.DeclarationContext)
                            {
                                if (!_context.DeclaredVarNames.Contains(d.ToString()))
                                {
                                    sb.Append("private object ");
                                    sb.Append(d.ToString());
                                    sb.Append("{get;set;}");
                                    _context.DeclaredVarNames.Add(d.ToString());
                                }
                            }
                            else
                            {
                                sb.Append(d.ToString());
                                sb.Append(";");
                            }

                            if (_debugMode && !this.DeclarationContext)
                                sb.Append("Debugger.BreakPoint(this);");
                        }
                    }
                    break;

                    #endregion

                    #region "VariableDeclarator"

                case "VariableDeclarator":
                    var variableDeclarator = (syntax as VariableDeclarator);

                    if (this.ReturnContext)
                    {
                        sb.Append(variableDeclarator.Id);
                    }
                    else
                    {
                        sb.Append(variableDeclarator.Id);
                        if (!this.DeclarationContext)
                        {
                            sb.Append(" = ");
                            if (variableDeclarator.Init != null)
                            {
                                sb.Append(variableDeclarator.Init);
                            }
                            else
                            {
                                sb.Append("JsObject.Undefined");
                            }
                        }
                    }
                    break;

                    #endregion

                    #region "Identifier"

                case "Identifier":
                    var identifier = (syntax as Identifier);

                    var name = identifier.Name.Replace("$", "@");

                    if(name == "String" || name == "string")
                    {
                        name = "@" + name;
                    }

                    if (this.DeclarationContext)
                        sb.Append(name);
                    else
                        sb.Append("((dynamic)this)." + name);

                    break;

                    #endregion

                    #region "Literal"

                case "Literal":
                    var literal = (syntax as Literal);
                    if (literal.IsString) sb.Append("@\"");
                    sb.Append(literal.Value.Replace("\"", "\"\""));
                    if (literal.IsString) sb.Append("\"");

                    break;

                    #endregion

                    #region "ExpressionStatement"

                case "ExpressionStatement":
                    var expression = (syntax as ExpressionStatement);
                    if (_debugMode && !this.DeclarationContext)
                        sb.Append(string.Format("Debugger.BreakPoint({0}, {1}, {2}, {3}, {4}, {5});",
                                                expression.Range.Start, expression.Range.End, expression.Loc.Start.Line,
                                                expression.Loc.Start.Column, expression.Loc.End.Line,
                                                expression.Loc.End.Column));

                    sb.Append(expression.Expression.ToString());
                    sb.Append(";");

                    if (_debugMode && !this.DeclarationContext)
                        sb.Append("Debugger.BreakPoint(this);");

                    break;

                    #endregion

                    #region "AssignmentExpression"

                case "AssignmentExpression":
                    var assign = (syntax as AssignmentExpression);
                    sb.Append(assign.Left.ToString());
                    sb.Append(string.Format(" {0} ", assign.Operator));
                    sb.Append(assign.Right.ToString());

                    break;

                    #endregion

                    #region "BinaryExpression"

                case "BinaryExpression":
                    var binary = (syntax as BinaryExpression);
                    sb.Append("(");
                    sb.Append(binary.Left.ToString());

                    var op = binary.Operator;

                    if (binary.Operator == "!==")
                    {
                        op = "!=";
                    }
                    else if (binary.Operator == "===")
                    {
                        op = "==";
                    }
                    else if (binary.Operator == "!=")
                    {
                        sb.Append(".ToString()");
                    }
                    else if (binary.Operator == "==")
                    {
                        sb.Append(".ToString()");
                    }

                    sb.Append(string.Format(" {0} ", op));
                    sb.Append(binary.Right.ToString());

                    if (binary.Operator == "!=" || binary.Operator == "==")
                        sb.Append(".ToString()");

                    sb.Append(")");

                    break;

                    #endregion

                    #region "CallExpression"

                case "CallExpression":
                    var call = (syntax as CallExpression);
                    sb.Append(call.Callee.ToString());
                    sb.Append("(");
                    call.Arguments.ToList().ForEach(a => sb.Append(a.ToString()));
                    sb.Append(")");
                    break;

                    #endregion

                    #region "IfStatement"

                case "IfStatement":
                    var @if = (syntax as IfStatement);
                    sb.Append(string.Format("if({0})", @if.Test.ToString()));
                    sb.Append("{");
                    sb.Append(@if.Consequent.ToString());
                    sb.Append("}");

                    if (@if.Alternate != null)
                    {
                        sb.Append("else");
                        sb.Append("{");
                        sb.Append(@if.Alternate.ToString());
                        sb.Append("}");
                    }

                    break;

                    #endregion

                    #region "BlockStatement"

                case "BlockStatement":
                    var block = (syntax as BlockStatement);
                    block.Body.ForEach(b => sb.Append(b.ToString()));

                    break;

                    #endregion

                    #region "EmptyStatement"

                case "EmptyStatement":
                    sb.Append("");
                    break;

                    #endregion

                    #region "UpdateExpression"

                case "UpdateExpression":
                    var update = (syntax as UpdateExpression);
                    //if (update.Prefix)
                    //{
                    //    sb.Append(update.Operator);
                    //}

                    //sb.Append(update.Argument.ToString());
                    sb.AppendFormat("{0} = JsObject.PlusPlus({0}, {1})", update.Argument.ToString(), update.Prefix.ToString().ToLower());

                    //if (!update.Prefix)
                    //{
                    //    sb.Append(update.Operator);
                    //}

                    break;

                    #endregion

                #region "MemberExpression"

                case "MemberExpression":
                    var member = (syntax as MemberExpression);
                    //sb.AppendFormat("((dynamic){0}).{1}", member.Object.ToString(), member.Property.ToString());
                    sb.Append(member.Property.ToString());

                    break;

                #endregion

                #region "ArrayExpression"

                case "ArrayExpression":
                    var array = (syntax as ArrayExpression);
                    sb.Append("new object[]{");

                    sb.Append(string.Join(", ", array.Elements.Select(el => el.ToString())));

                    sb.Append("}");

                    break;

                #endregion
            }

            return sb.ToString();
        }
    }
}