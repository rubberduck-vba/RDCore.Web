# 8.0 JSON Values

An endpoint passes a procedure's arguments as JSON and receives its returned value as JSON. This chapter defines how a VBA value and a JSON value convert
to each other. It is an RD-VBA addition with no MS-VBAL counterpart.

> [!NOTE]
> **Not implemented.** An endpoint passes the procedure's name and its JSON arguments to a machine, but no machine runs the procedure in an RDCore runtime
> session. So no endpoint's invocation converts its arguments or its value. VBA code that references the [`RDJson` library](#85-the-rdjson-library)
> runs the same conversions.

A value that does not convert, in either direction, is run-time error 13 (Type mismatch), unless a row below names another error.

---
## 8.1 From VBA to JSON

A procedure's returned value converts as follows.

|VBA value|JSON value|
|---|---|
|`Boolean`|`true` or `false`.|
|`Byte`, `Integer`, `Long`, `LongLong`, `Single`, `Double`|A number.|
|`Currency`, `Decimal`|A number, written with every digit. Both types are exact decimals ([**MS-VBAL §2.1** Data Values and Value Types](https://learn.microsoft.com/en-us/openspecs/microsoft_general_purpose_programming_languages/ms-vbal/c86480b2-aef2-4488-b177-f55e13cc51f2)), and so is a JSON number's text.|
|`Date`|A string: ISO 8601 to the second, with no offset, such as `"2026-11-26T14:30:00"`. A `Date` has no time zone.|
|`String`, fixed-length `String`|A string.|
|`Empty`, `Null`, `Nothing`|`null`.|
|An array|An array of its elements. A multi-dimensional array is nested arrays, its first dimension outermost. Bounds are not kept, and an array with no elements is `[]`.|
|A `Scripting.Dictionary`|An object: each key a member name, each item its value, in the order of the keys. A key that is not a `String` is error 13.|
|A `Collection`|An array of its items.|
|An object of a class of the workspace|An object of its public members ([8.4](#84-objects)).|
|Any other object, a UDT, an `Error` value|Error 13.|

- Elements, items and members convert by the same rules, at any depth. A `Variant` converts as the value it holds.
- An object held twice is written twice. An object that holds itself, directly or through another, is error 13.
- The procedure has already run when its value fails to convert: the invocation ends in error 13, after any effect the procedure had.

A `Dictionary` keeps `1` and `"1"` as two keys, and a JSON object cannot; converting other keys would merge them.

---
## 8.2 From JSON to VBA

A JSON value has a natural VBA value, which is what a `Variant` parameter receives.

|JSON value|VBA value|
|---|---|
|`true`, `false`|`True`, `False`.|
|A number without a fraction or an exponent|By its magnitude, as an integer literal without a type suffix ([**MS-VBAL §3.3.2** Number Tokens](https://learn.microsoft.com/en-us/openspecs/microsoft_general_purpose_programming_languages/ms-vbal/685ad840-accb-4bdb-8bfd-f3d88498547a)): `Integer` to 32767, `Long` to 2147483647, `Double` past it. The sign is not part of the literal, so `-32768` is a `Long`.|
|Any other number|`Double`.|
|A string|`String`. ISO 8601 text stays a `String`.|
|`null`|`Null`.|
|An array|A `Variant()` with lower bound 0. A nested array is a `Variant()` element of it.|
|An object|A new `Scripting.Dictionary` of `String` keys, its `CompareMode` `vbBinaryCompare`: JSON member names are case-sensitive.|

A parameter of another declared type receives the following.

|Parameter's declared type|Argument|
|---|---|
|`Object`, `Dictionary`|An object is a `Dictionary`, and `null` is `Nothing`. Anything else is error 13.|
|A class of the workspace|An object is a `New` object of the class, filled from its keys ([8.4](#84-objects)), and `null` is `Nothing`. Anything else is error 13.|
|`Collection`|An array is a `New Collection` of its elements, each its natural value, and `null` is `Nothing`. Anything else is error 13.|
|Another class|`null` is `Nothing`. Anything else is error 13.|
|A dynamic array|An array is an array of exactly the element type, with lower bound 0, each element converted as a parameter of that type is. A `Variant()` holds natural values. An element that does not convert is the array's error. Anything else is error 13.|
|`Date`|A string is read as ISO 8601 to the second, with no offset. Any other string is error 13: one with a zone or an offset, or a date alone.|
|`Currency`, `Decimal`|A number is read from its digits, so none is lost to a `Double`, then let-coerced.|
|Another numeric type, `Boolean`, `String`|The natural value, let-coerced ([**RD-VBAL §5.5.1.2** Runtime semantics](https://rubberduck-vba.github.io/RDCore/specs/rd-vbal.5.5.1.2.runtime-semantics.html)): error 13 for text that is not a number, error 6 (Overflow) past the type's range.|
|An `Enum`|As a `Long`, since an `Enum` member is a `Long` ([**MS-VBAL §5.2.3.4** Enum Declarations](https://learn.microsoft.com/en-us/openspecs/microsoft_general_purpose_programming_languages/ms-vbal/da1d4885-946f-4937-9487-488143b97f08)).|
|Any other type: a fixed-size array, an array of a UDT, a UDT|Error 13.|

`null` to any type other than `Variant` or an object type is let-coercion from `Null`
([**RD-VBAL §5.5.1.2.10** Let-coercion from Null](https://rubberduck-vba.github.io/RDCore/specs/rd-vbal.5.5.1.2.runtime-semantics.html#551210-let-coercion-from-null)):
error 13 for an array or a UDT, and error 94 (Invalid use of Null) for any other type.

A nested JSON array is not read as a multi-dimensional array: its rows may differ in length, and `Array(Array(...))` is how VBA nests lists.

---
## 8.3 Arguments

The procedure is a `Public` `Sub`, `Function` or `Property Get` of a standard module, named with its module. Arguments are named, and names match
parameters case-insensitively, as VBA names do ([**MS-VBAL §5.3.1.11** Procedure Invocation Argument Processing](https://learn.microsoft.com/en-us/openspecs/microsoft_general_purpose_programming_languages/ms-vbal/1fb9af32-fc48-4c4f-998a-ed8047048ca5)).

|Situation|Run-time error, before the procedure runs|
|---|---|
|An argument names no parameter or names the `ParamArray`, or two name the same one in different cases|448 (Named argument not found).|
|A parameter that is not `Optional` has no argument|449 (Argument not optional).|
|An argument does not convert to its parameter's type|The error of its conversion, above.|

An `Optional` parameter without an argument takes its default value, or is missing when it is a `Variant` with no default. A `ParamArray` is given no
arguments.

The objects made for the arguments are let go of when the invocation is over. An object the program did not keep is destroyed then, and its
`Class_Terminate` runs.

---
## 8.4 Objects

An object of a class of the workspace converts by its members, which are read and given values as `CallByName` reads them and gives them values.

### Writing an object

- Its members are its `Public` variables and its `Public` `Property Get` procedures that take no argument, in the order the class declares them, each
  named as it is declared.
- `Friend` and `Private` members, methods, a `Property Let` or `Set` with no `Get`, and a `Get` that takes arguments take no part. JSON is outside the
  project, which is the only code a `Friend` member is reachable from.
- Each `Property Get` runs. An error it raises ends the conversion in that error.

### Filling an object

- A JSON object for a class of the workspace is a `New` object of the class, so its `Class_Initialize` runs first.
- Each key then gives its value to the member it names, in the order of the keys: a `Public` variable, or a `Property Let` or `Property Set` that takes
  only the value. The value converts to that member's type by [8.2](#82-from-json-to-vba), so a member `As Customer` is a `Customer` filled the same way.
  With both a `Let` and a `Set`, an object or `null` is given through the `Set`.
- Keys match member names case-insensitively, as VBA names do. Two keys that name one member in different cases are error 457 (This key is already
  associated with an element of this collection).
- An error that `Class_Initialize` or a member raises ends the conversion in that error, and an `End` in one ends the invocation.

A key or a member that does not match is handled by default as the table says. A [`JsonSerializationOptions`](#85-the-rdjson-library) object given to
`JsonValue.Deserialize` makes each case raise the error `CallByName` raises for it instead.

|Case|Default|Option|Error when it raises|
|---|---|---|---|
|A key that names no public member, or a `Friend` or a `Private` one|Ignored.|`UnknownMembers`|438 (Object doesn't support this property or method).|
|A key that names a method, or a `Property Let` or `Set` that takes arguments besides its value|Ignored.|`UnknownMembers`|450 (Wrong number of arguments or invalid property assignment).|
|A key that names a `Property Get` with no `Let` or `Set`|Ignored, so a client that echoes back what it was given is not refused.|`ReadOnlyMembers`|451 (Property let procedure not defined and property get procedure did not return an object).|
|A member that can be given a value and that no key names|Keeps its value.|`MissingMembers`|449 (Argument not optional).|

The keys are checked before any member is given a value, so a JSON object that raises changes nothing. The options hold for the objects of members too.
A procedure's arguments are always filled with the default of each case.

---
## 8.5 The RDJson library

A project that references `RDJson` converts its own values with it. `RDJson` is defined by the platform: no type library describes it, and a reference
finds it by its name.

|Member|Description|
|---|---|
|`JsonValue.Serialize(Value As Variant, [Options As JsonSerializationOptions]) As String`|The JSON text of the value, by [8.1](#81-from-vba-to-json). `Options` has no effect on it. In a string, each character outside ASCII, and `<`, `>`, `&`, `'`, `+`, `` ` `` and `"`, is written as a `\u` escape.|
|`JsonValue.Deserialize(Json As String, [Into As Object], [Options As JsonSerializationOptions]) As Variant`|The natural value of the JSON text, by [8.2](#82-from-json-to-vba). Text that is not JSON is error 5 (Invalid procedure call or argument). With `Into`, the object it is given, filled from the JSON object by [8.4](#84-objects), and returned. `Into` is an object of a class of the workspace, and the text a JSON object: anything else is error 13.|
|`JsonSerializationOptions`|A class whose `UnknownMembers`, `MissingMembers` and `ReadOnlyMembers` are each a `JsonMemberHandling`, `jsonIgnore` in a new object ([8.4](#84-objects)). Any other value is error 380 (Invalid property value).|
|`JsonMemberHandling`|An enumeration: `jsonIgnore` (0) or `jsonRaise` (1).|

`Deserialize` with an object the program has patches it: only the members the JSON names change, and the others keep their values. For a class
`Order` that declares `Public Note As String`:

```vba
Public Function Rushed(ByVal body As String) As String
    Dim options As JsonSerializationOptions
    Set options = New JsonSerializationOptions
    options.UnknownMembers = jsonRaise

    Dim placed As Order
    Set placed = JsonValue.Deserialize(body, New Order, options)
    Set placed = JsonValue.Deserialize("{""Note"":""Rush""}", placed)
    Rushed = JsonValue.Serialize(placed)
End Function
```

---
## Implementation

The conversions are RDCore's runtime, in the `RDCore.Runtime` assembly, and `RDJson` is described by `Symbols/RDJson.json` in RDCore.

|Type or member|Role|
|---|---|
|`JsonValues`|Converts a value both ways: `ToJson` for a returned value, `FromJson` for an argument and its parameter's declared type, or for an object to fill. A conversion that fails returns its run-time error.|
|`JsonMemberRules`|Which of the cases of [8.4](#84-objects) raise.|
|`JsonCallProvider`|Runs the members of `RDJson`.|
|`JsonValuesTests`, `JsonInstancesTests`, `JsonLibraryTests`|The tests, in RDCore's tests: the conversions both ways, objects and arrays given to and returned by a procedure, and `RDJson` called from VBA code.|
