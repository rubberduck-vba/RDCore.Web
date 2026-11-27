# 8.0 JSON Values

An endpoint passes a procedure's arguments as JSON and receives its returned value as JSON. This chapter defines how a VBA value and a JSON value convert
to each other. It is an RD-VBA addition with no MS-VBAL counterpart.

> [!NOTE]
> **Not implemented.** Nothing invokes a procedure by name with JSON arguments yet, so no invocation converts its arguments or its value, and the
> [Arguments](#83-arguments) rules apply nowhere.

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
|Any other object, a UDT, an `Error` value|Error 13.|

- Elements, items and members convert by the same rules, at any depth. A `Variant` converts as the value it holds.
- An object held twice is written twice. A `Dictionary` or a `Collection` that contains itself, directly or through another, is error 13.
- The procedure has already run when its value fails to convert: the invocation ends in error 13, after any effect the procedure had.

A `Dictionary` keeps `1` and `"1"` as two keys, and a JSON object cannot; converting other keys would merge them. An object other than a `Dictionary` or a
`Collection` does not convert, because reading its properties would run its `Property Get` procedures.

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
|Another class|`null` is `Nothing`. Anything else is error 13.|
|`Variant()`|An array is a `Variant()`, as in a `Variant`. Anything else is error 13.|
|`Date`|A string is read as ISO 8601 to the second, with no offset. Any other string is error 13: one with a zone or an offset, or a date alone.|
|`Currency`, `Decimal`|A number is read from its digits, so none is lost to a `Double`, then let-coerced.|
|Another numeric type, `Boolean`, `String`|The natural value, let-coerced ([**RD-VBAL §5.5.1.2** Runtime semantics](https://rubberduck-vba.github.io/RDCore/specs/rd-vbal.5.5.1.2.runtime-semantics.html)): error 13 for text that is not a number, error 6 (Overflow) past the type's range.|
|Any other type, such as an array of another element type or a UDT|Error 13.|

`null` to any type other than `Variant` or an object type is let-coercion from `Null`
([**RD-VBAL §5.5.1.2.10** Let-coercion from Null](https://rubberduck-vba.github.io/RDCore/specs/rd-vbal.5.5.1.2.runtime-semantics.html#551210-let-coercion-from-null)):
error 13 for a `Variant()` or a UDT, and error 94 (Invalid use of Null) for any other type.

A nested JSON array is not read as a multi-dimensional array: its rows may differ in length, and `Array(Array(...))` is how VBA nests lists.

---
## 8.3 Arguments

The procedure is a `Public` `Sub`, `Function` or `Property Get`, named with its module. Arguments are named, and names match parameters case-insensitively,
as VBA names do ([**MS-VBAL §5.3.1.11** Procedure Invocation Argument Processing](https://learn.microsoft.com/en-us/openspecs/microsoft_general_purpose_programming_languages/ms-vbal/1fb9af32-fc48-4c4f-998a-ed8047048ca5)).

|Situation|Run-time error, before the procedure runs|
|---|---|
|An argument names no parameter, or two name the same one in different cases|448 (Named argument not found).|
|A parameter that is not `Optional` has no argument|449 (Argument not optional).|
|An argument does not convert to its parameter's type|The error of its conversion, above.|

An `Optional` parameter without an argument takes its default value, or is missing when it is a `Variant` with no default.

---
## Implementation

The conversions are RDCore's runtime, in the `RDCore.Runtime` assembly.

|Type or member|Role|
|---|---|
|`JsonValues`|Converts a value both ways: `ToJson` for a returned value, `FromJson` for an argument and its parameter's declared type. A conversion that fails returns its run-time error.|
|`JsonValuesTests`|The tests of the conversions, both ways, in RDCore's tests.|
