﻿namespace Zyan.SafeDeserializationHelpers
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Security.Permissions;
    using System.Security.Principal;

    /// <summary>
    /// Deserialization surrogate for the WindowsIdentity class.
    /// </summary>
    internal class WindowsIdentitySurrogate : ISerializationSurrogate
    {
        private static ConstructorInfo Constructor { get; } = typeof(WindowsIdentity).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null,
            new[] { typeof(SerializationInfo), typeof(StreamingContext) },
            null);

        /// <inheritdoc cref="ISerializationSurrogate" />
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            var ds = obj as ISerializable;
            ds.GetObjectData(info, context);
        }

        /// <inheritdoc cref="ISerializationSurrogate" />
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            Validate(info, context);

            // discard obj
            var result = Constructor.Invoke(new object[] { info, context });
            return result;
        }

        private void Validate(SerializationInfo info, StreamingContext context)
        {
            // check the serialized data using a guarded BinaryFormatter
            var fmt = new BinaryFormatter().Safe();

            var e = info.GetEnumerator();
            while (e.MoveNext())
            {
                switch (e.Name)
                {
                    case "System.Security.ClaimsIdentity.actor":
                    case "System.Security.ClaimsIdentity.claims":
                    case "System.Security.ClaimsIdentity.bootstrapContext":
                        var base64 = info.GetString(e.Name);
                        if (string.IsNullOrEmpty(base64))
                        {
                            continue;
                        }

                        // safe BinaryFormatter will throw on malicious payload
                        var buffer = Convert.FromBase64String(base64);
                        using (var ms = new MemoryStream(buffer))
                        {
                            fmt.Deserialize(ms);
                        }

                        break;
                }
            }
        }
    }
}
