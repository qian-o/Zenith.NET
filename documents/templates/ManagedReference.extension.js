exports.postTransform = function (model) {
    const labels = {
        classes: 'Classes', structs: 'Structs', interfaces: 'Interfaces', enums: 'Enums',
        delegates: 'Delegates', constructors: 'Constructors', fields: 'Fields', properties: 'Properties',
        methods: 'Methods', events: 'Events', operators: 'Operators', eii: 'Explicit implementations'
    };
    const kind = type => type ? type.charAt(0).toUpperCase() + type.slice(1) : 'Type';
    model.apiKind = kind(model.type);
    model.apiName = model.name?.[0]?.value || model.uid;
    model.apiHasMemberFinder = !model.isNamespace;
    model.apiSeeAlsoId = 'seealso';
    model.apiHasRelationships = ['inheritance', 'implements', 'derivedClasses', 'inheritedMembers', 'extensionMethods']
        .some(key => Array.isArray(model[key]) && model[key].length > 0);
    for (const group of model.children || []) {
        group.apiTitle = model.isEnum ? 'Values' : labels[group.id] || group.id.replace(/[-_]/g, ' ');
        for (const item of group.children || []) {
            item.apiSeeAlsoId = item.id + '_seealso';
        }
    }
    if (model.namespace?.uid && model.namespace.specName) {
        for (const name of model.namespace.specName) {
            name.value = `<a class="xref" href="${model.namespace.uid}.html">${model.namespace.uid}</a>`;
        }
    }

    return model;
};
