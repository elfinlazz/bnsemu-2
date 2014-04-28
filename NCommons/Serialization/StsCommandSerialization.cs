using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq.Expressions;
using System.Xml;
using NCommons.Network.StsCommands;

namespace NCommons.Serialization
{
    static class StsCommandSerialization
    {

        public static void GenerateStsCommandDelegates<TCommand>(
            string headerElementName,
            out Action<XmlWriter, TCommand> writeDelegate,
            out Action<XmlReader, TCommand> readDelegate)
            where TCommand : StsCommand<TCommand>, new()
        {
            Type t = typeof(TCommand);


            var paramCommand = Expression.Parameter(typeof(TCommand), "command");
            var paramWriter = Expression.Parameter(typeof(XmlWriter), "writer");
            var paramReader = Expression.Parameter(typeof(XmlReader), "reader");
            var varDoc = Expression.Variable(typeof(XmlDocument), "doc"); // used by reader
            var varNode = Expression.Variable(typeof(XmlNode), "node"); // used in reader loop

            List<Expression> writerExpressions = new List<Expression>();
            List<Expression> readerExpressions = new List<Expression>();

            // TODO: Isn't there any way to easily get the MethodInfo for these functions?

            // write header element
            writerExpressions.Add(Expression.Call(paramWriter, "WriteStartElement", null, Expression.Constant(headerElementName)));

            // read header element
            readerExpressions.Add(Expression.Call(paramReader, "MoveToContent", null));
            readerExpressions.Add(Expression.Call(paramReader, "ReadStartElement", null, Expression.Constant(headerElementName)));
            readerExpressions.Add(Expression.Assign(varDoc, Expression.New(typeof(XmlDocument))));

            List<Expression> readerLoopExpressions = new List<Expression>();

            // add readerloop end check. if (!(reader.Read() && (reader.NodeType != XmlNodeType.EndElement))) break;
            // this should basically break out of the loop whenever this condition is met.
            var label = Expression.Label();
            readerLoopExpressions.Add(
                Expression.IfThen(
                    Expression.Not(
                //Expression.Block(
                        Expression.AndAlso(
                            Expression.Call(paramReader, "Read", null),
                // verify the XmlNodeType stuff here, this may be wrong.
                            Expression.NotEqual(Expression.PropertyOrField(paramReader, "NodeType"), Expression.Constant(XmlNodeType.EndElement, typeof(XmlNodeType))))),
                    Expression.Break(label))
                );

            // initial XmlNode creation on each iteration
            readerLoopExpressions.Add(
                Expression.Assign(
                    varNode,
                    Expression.Call(varDoc, "ReadNode", null, paramReader)));

            FieldInfo[] fields = t.GetFields();
            for (int i = 0; i < fields.Length; i++)
            {
                // NOTE: We will probably need to add things like arrays or big chunks of data, this is probably incomplete.

                CommandFieldAttribute attrField;
                if ((attrField = fields[i].GetCustomAttribute<CommandFieldAttribute>()) != null)
                {
                    // TODO: Arrays/Structures, these can't just be "tostring".

                    // writer
                    var writerExpr =
                        Expression.Call(
                        paramWriter,
                        "WriteElementString",
                        null,
                        Expression.Constant(fields[i].Name),
                        Expression.Call(Expression.PropertyOrField(paramCommand, fields[i].Name), "ToString", null));

                    if (attrField.Optional == true)
                    {
                        // If parameter is optional, only write if not null
                        writerExpressions.Add(
                            Expression.IfThen(
                                Expression.NotEqual(
                                    Expression.Field(paramCommand, fields[i]),
                                    Expression.Default(fields[i].FieldType)), // is this alright?
                            //Expression.Field(paramCommand, fields[i].Name),
                            //Expression.Constant(null, fields[i].FieldType)), // will that work?
                                writerExpr));
                    }
                    else
                    {
                        writerExpressions.Add(writerExpr);
                    }

                    // reader
                    Expression readerAssignRightExpr = Expression.Property(varNode, "InnerText");

                    // TODO: Nullables/Arrays/ect
                    // There can be array of "sub-structures"...
                    // TODO: Handle all nullables by first doing the same nullity check.

                    Type valType = fields[i].FieldType;
                    if (valType.IsGenericType && valType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        valType = fields[i].FieldType.GetGenericArguments()[0];

                    // TODO: TryParse, we can make it "automatic" with all value types too.
                    if (valType == typeof(UInt32))
                    {
                        Func<String, UInt32> f = UInt32.Parse;
                        readerAssignRightExpr = Expression.Call(f.Method, Expression.Call(Expression.Property(varNode, "InnerText"), "Trim", null));
                    }
                    else if (valType == typeof(Int32))
                    {
                        Func<String, Int32> f = Int32.Parse;
                        readerAssignRightExpr = Expression.Call(f.Method, Expression.Call(Expression.Property(varNode, "InnerText"), "Trim", null));
                    }
                    else if (valType == typeof(String))
                    {
                        readerAssignRightExpr = Expression.Call(Expression.Property(varNode, "InnerText"), "Trim", null);
                    }
                    else
                    {
                        // temporary
                        throw new ApplicationException("Unhandled type in StsCommand serialization generator.");
                    }

                    Expression ex;

                    // TESTING
                    if (fields[i].FieldType.IsGenericType && fields[i].FieldType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        ex = Expression.Assign(Expression.Field(paramCommand, fields[i]), Expression.TypeAs(readerAssignRightExpr, fields[i].FieldType));
                    /*ex = Expression.IfThen(
                        Expression.Equal(readerAssignRightExpr, Expression.Constant(null, fields[i].FieldType)),
                        Expression.Assign(Expression.Field(paramCommand, fields[i]), Expression.TypeAs(readerAssignRightExpr, fields[i].FieldType)));*/
                    else
                        ex = Expression.Assign(Expression.Field(paramCommand, fields[i]), readerAssignRightExpr);

                    readerLoopExpressions.Add(Expression.IfThen(Expression.Equal(Expression.Property(varNode, "Name"), Expression.Constant(fields[i].Name)), ex));
                }
            }

            readerExpressions.Add(Expression.Loop(Expression.Block(readerLoopExpressions), label));
            writerExpressions.Add(Expression.Call(paramWriter, "WriteEndElement", null));

            // Kinda temporary. We will need to have sub-structures like Packet<>
            Expression writerExpression = Expression.Block(writerExpressions);
            Expression readerExpression = Expression.Block(new ParameterExpression[] { varDoc, varNode }, readerExpressions);

            writeDelegate = Expression.Lambda<Action<XmlWriter, TCommand>>(writerExpression, paramWriter, paramCommand).Compile();
            readDelegate = Expression.Lambda<Action<XmlReader, TCommand>>(readerExpression, paramReader, paramCommand).Compile();
        }
    }
}
