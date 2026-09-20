const groupTitles = {
    classes: 'Classes', structs: 'Structs', interfaces: 'Interfaces', enums: 'Enums',
    delegates: 'Delegates', constructors: 'Constructors', fields: 'Fields', properties: 'Properties',
    methods: 'Methods', events: 'Events', operators: 'Operators', eii: 'Explicit implementations'
};
const relationshipKeys = ['inheritance', 'implements', 'derivedClasses', 'inheritedMembers', 'extensionMethods'];

function prepareSignature(syntax) {
    if (!syntax) return;
    const value = syntax.fieldValue || syntax.propertyValue || syntax.eventType || syntax.return;
    if (value) {
        syntax.apiValueReference = value.type?.specName?.[0]?.value;
        syntax.apiResult = {
            label: syntax.return ? 'Returns' : 'Value',
            type: syntax.apiValueReference,
            description: value.description,
            showType: !syntax.content?.[0]?.value
        };
    }
    syntax.apiParameterDescriptions = (syntax.parameters || []).filter(parameter => parameter.description?.trim());
    syntax.apiTypeParameterDescriptions = (syntax.typeParameters || []).filter(parameter => parameter.description?.trim());
}

function uidSuffix(uid) {
    let hash = 2166136261;
    for (let index = 0; index < uid.length; index++) hash = Math.imul(hash ^ uid.charCodeAt(index), 16777619);
    return (hash >>> 0).toString(16);
}

exports.postTransform = function (model) {
    model.apiName = model.name?.[0]?.value || model.uid;
    model.apiHasMemberFinder = !model.isNamespace;
    model.apiSeeAlsoId = 'seealso';
    model.apiHasRelationships = relationshipKeys.some(key => model[key]?.length > 0);
    model.apiHasTypeDetails = !model.isNamespace && (model.apiHasRelationships || !!model.namespace?.uid || !!model.assemblies?.length);
    model.apiTypeDetailsLabel = model.apiHasRelationships ? 'Inheritance and metadata' : 'Type metadata';
    prepareSignature(model.syntax);
    if (model.syntax) {
        model.syntax.apiBaseReferences = [...(model.inheritance || []), ...(model.implements || [])]
            .map(type => ({ type: type.specName?.[0]?.value })).filter(reference => reference.type);
    }

    const anchors = new Set([model.id]);
    const overloads = new Set();
    for (const group of model.children || []) {
        group.apiTitle = model.isEnum ? 'Values' : groupTitles[group.id] || group.id.replace(/[-_]/g, ' ');
        for (const item of group.children || []) {
            item.apiName = item.name?.[0]?.value || item.uid;
            // DocFX normalizes pointer/by-ref UIDs to identical IDs in some overloads.
            // Keep the first canonical anchor; later members get stable local anchors.
            let anchor = item.id;
            if (anchors.has(anchor)) anchor += '_' + uidSuffix(item.uid);
            item.apiAnchor = anchor;
            anchors.add(anchor);
            item.apiSeeAlsoId = anchor + '_seealso';
            if (item.overload && !overloads.has(item.overload.id)) {
                item.apiOverload = item.overload;
                overloads.add(item.overload.id);
            }
            prepareSignature(item.syntax);
        }
    }
    if (model.namespace?.uid && model.namespace.specName) {
        for (const name of model.namespace.specName) {
            name.value = `<a class="xref" href="${model.namespace.uid}.html">${model.namespace.uid}</a>`;
        }
    }
    return model;
};
