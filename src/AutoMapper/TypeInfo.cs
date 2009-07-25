using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AutoMapper
{
    public class TypeInfo
    {
        private readonly IMemberAccessor[] _publicReadAccessors;
        private readonly MethodInfo[] _publicGetMethods;

        public Type Type { get; private set; }

        public TypeInfo(Type type)
        {
            Type = type;
            _publicReadAccessors = BuildPublicReadAccessors();
            _publicGetMethods = BuildPublicNoArgMethods();
        }

        public IEnumerable<IMemberAccessor> GetPublicReadAccessors()
        {
            return _publicReadAccessors;
        }

        public IEnumerable<MethodInfo> GetPublicNoArgMethods()
        {
            return _publicGetMethods;
        }

        private IMemberAccessor[] BuildPublicReadAccessors()
        {
            // Collect that target type, its base type, and all implemented/inherited interface types
            IEnumerable<Type> typesToScan = new[] { Type, Type.BaseType };

            if (Type.IsInterface)
                typesToScan = typesToScan.Concat(Type.FindInterfaces((m, f) => true, null));

            // Scan all types for public properties and fields
            var allMembers = typesToScan
                .Where(x => x != null) // filter out null types (e.g. type.BaseType == null)
                .SelectMany(x => x.FindMembers(MemberTypes.Property | MemberTypes.Field,
                                                 BindingFlags.Instance | BindingFlags.Public,
                                                 (m, f) =>
                                                 m is FieldInfo ||
                                                 (m is PropertyInfo && ((PropertyInfo)m).CanRead && !((PropertyInfo)m).GetIndexParameters().Any()), null));

            // Multiple types may define the same property (e.g. the class and multiple interfaces) - filter this to one of those properties
            var filteredMembers = allMembers
                .OfType<PropertyInfo>()
                .GroupBy(x => x.Name) // group properties of the same name together
                .Select(x =>
                    x.Any(y => y.CanRead && y.CanWrite) ? // favor the first property that can both read & write - otherwise pick the first one
                        x.Where(y => y.CanRead && y.CanWrite).First() :
                        x.First())
                .OfType<MemberInfo>() // cast back to MemberInfo so we can add back FieldInfo objects
                .Concat(allMembers.Where(x => x is FieldInfo));  // add FieldInfo objects back

            return filteredMembers
                .Select(x => x.ToMemberAccessor())
                .ToArray();
        }

        private MethodInfo[] BuildPublicNoArgMethods()
        {
            return Type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => (m.ReturnType != null) && (m.GetParameters().Length == 0) && (m.MemberType == MemberTypes.Method))
                .ToArray();
        }
    }
}