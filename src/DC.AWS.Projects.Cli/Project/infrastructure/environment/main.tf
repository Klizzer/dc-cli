variable "aws_region" {}
variable "domain_name" {}

provider "aws" {
  region = var.aws_region
}

resource "aws_cloudformation_stack" "[[PROJECT_NAME]]" {
  name = "[[PROJECT_NAME]]-${terraform.workspace}"

  parameters = {
    DomainName = var.domain_name
  }

  capabilities = ["CAPABILITY_IAM", "CAPABILITY_AUTO_EXPAND"]

  template_body = file("./.generated/project.packaged.yml")
}
