var ControlGuidesController = pc.createScript('controlGuidesController');


ControlGuidesController.attributes.add('leftClickIcon', {
    type: 'entity',
    description: 'Left Click Icon'
});

ControlGuidesController.attributes.add('rightClickIcon', {
    type: 'entity',
    description: 'Right Click Icon'
});

ControlGuidesController.attributes.add('movementIcon', {
    type: 'entity',
    description: 'Movement Icon'
});

ControlGuidesController.prototype.initialize = function()
{
    this.enableLeftClick(false);
    this.enableRightClick(false);
    this.enableMovementGuides(true);
}

ControlGuidesController.prototype.enableLeftClick = function(enable)
{
    this.leftClickIcon.enabled = enable;
}

ControlGuidesController.prototype.enableRightClick = function(enable)
{
    this.rightClickIcon.enabled = enable;
}

ControlGuidesController.prototype.enableMovementGuides = function(enable)
{
    this.movementIcon.enabled = enable;
}