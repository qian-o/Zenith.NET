const resources = require('./resources.common.js');
exports.postTransform = function (model) {
    const strings = resources.source(model);
    const visit = item => {
        if (item.name?.startsWith('@')) {
            item.resourceKey = item.name.slice(1);
            item.name = resources.get(strings, item.resourceKey);
        }
        for (const child of item.items || []) visit(child);
    };
    visit(model);
    return model;
};
