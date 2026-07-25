exports.postTransform = function (model) {
  if (!model.namespace?.uid || !model.namespace.specName) {
    return model;
  }

  for (const name of model.namespace.specName) {
    name.value = `<a class="xref" href="${model.namespace.uid}.html">${model.namespace.uid}</a>`;
  }

  return model;
};
